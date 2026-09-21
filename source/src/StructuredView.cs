using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace VisualNovelHelper {
// Engine-neutral visible-state contract. Engine details stay in the bridge.
public sealed class ViewItem { public string Id, Text; }
public sealed class VisibleView {
    public string Session, Mode, Focus, Title, Phase;
    public long Sequence;
    public ViewItem Hover;
    public readonly List<ViewItem> Items=new List<ViewItem>();
    public string LayoutKey {
        get { var b=new StringBuilder(Mode).Append('\t').Append(Title); foreach(var i in Items) b.Append('\t').Append(i.Id).Append('\t').Append(i.Text); return b.ToString(); }
    }
}
public static class ViewProtocol {
    public static uint Checksum(string text) {
        uint a=1,b=0; foreach(char c in text) { a=(a+c)%65521; b=(b+a)%65521; } return b*65536+a;
    }
    static string Decode(string text) {
        return Regex.Replace(text,@"%([0-9A-F]{2})",m=> {
            switch(m.Groups[1].Value) { case "25":return "%"; case "09":return "\t"; case "0D":return "\r"; case "0A":return "\n"; default:throw new FormatException("Unknown escape"); }
        });
    }
    public static VisibleView Parse(string text) {
        if(text==null || text.Length>131072) return null;
        try {
            var lines=text.TrimEnd('\r','\n').Replace("\r\n","\n").Split('\n');
            if(lines.Length<4 || lines.Length>10004) return null;
            var head=lines[0].Split('\t'); var end=lines[lines.Length-1].Split('\t');
            long seq; uint checksum; int length;
            if(head.Length!=3 || head[0]!="VNH2" || !Regex.IsMatch(head[1],@"^\d+-\d+$") || !Int64.TryParse(head[2],out seq) || seq<1) return null;
            if(end.Length!=5 || end[0]!="END" || end[1]!=head[1] || end[2]!=head[2] || !Int32.TryParse(end[3],out length) || !UInt32.TryParse(end[4],out checksum)) return null;
            string body=String.Join("\n",lines,1,lines.Length-2);
            if(body.Length!=length || Checksum(body)!=checksum) return null;
            var view=new VisibleView { Session=head[1],Sequence=seq };
            var ids=new HashSet<string>();
            for(int n=1;n<lines.Length-1;n++) {
                var fields=lines[n].Split('\t');
                if(fields.Length==2 && fields[0]=="mode" && view.Mode==null) view.Mode=fields[1];
                else if(fields.Length==2 && fields[0]=="title" && view.Title==null) view.Title=Decode(fields[1]);
                else if(fields.Length==2 && fields[0]=="phase" && view.Phase==null) view.Phase=fields[1];
                else if(fields.Length==2 && fields[0]=="focus" && view.Focus==null) view.Focus=fields[1];
                else if(fields.Length==3 && fields[0]=="hover" && view.Hover==null && fields[1].Length>0 && !String.IsNullOrWhiteSpace(fields[2]))
                    view.Hover=new ViewItem {Id=fields[1],Text=Decode(fields[2])};
                else if(fields.Length==3 && fields[0]=="item" && fields[1].Length>0 && ids.Add(fields[1]))
                    view.Items.Add(new ViewItem {Id=fields[1],Text=Decode(fields[2])});
                else return null;
            }
            if(view.Mode!="dialogue" && view.Mode!="links" && view.Mode!="history" && view.Mode!="controls" && view.Mode!="passage" && view.Mode!="dialog" && view.Mode!="opening") return null;
            if(view.Mode=="opening" && (view.Items.Count!=1 || !Regex.IsMatch(view.Items[0].Id,@"^o:\d+$") || (view.Items[0].Text!="0" && view.Items[0].Text!="1"))) return null;
            if(view.Mode=="passage") {
                if(view.Items.Count!=1 || !Regex.IsMatch(view.Items[0].Id,@"^p:\d+$") || (view.Phase!="building" && view.Phase!="ready")) return null;
                if(view.Phase=="building" && view.Items[0].Text!="") return null;
                if(view.Phase=="ready" && String.IsNullOrWhiteSpace(view.Items[0].Text)) return null;
            } else if(view.Phase!=null) return null;
            if(view.Focus==null || (view.Focus!="" && !ids.Contains(view.Focus))) return null;
            return view;
        } catch(FormatException) { return null; }
    }
}
public sealed class ViewDecision { public bool Stop, IsDialogue; public string Text, ImageIdentity, ImageSlot; }
public sealed class ViewNarration {
    string session, layout, selection, pending, spoken, mode, title;
    string lastPassage, lastBuilding, lastOpening;
    DateTime since;
    bool focused, owns, pendingHover;
    string hoverId, hoverText; DateTime hoverSince;
    public bool OwnsNarration { get { return owns; } }
    static string Say(string text) {
        text=text.Trim();
        if(text.Length==0) return "Unlabelled option";
        if(Regex.IsMatch(text,@"^[.\u2026\s]+$")) return "Silence";
        return text;
    }
    public ViewDecision Update(VisibleView view, bool gameFocused, DateTime now) {
        var decision=UpdatePrimary(view,gameFocused,now);
        var target=view!=null && (view.Mode=="passage" || view.Mode=="dialogue") && gameFocused?view.Hover:null;
        string id=target==null?null:view.Session+"|"+target.Id;
        if(id!=hoverId) { hoverId=id; hoverText=target==null?null:target.Text; hoverSince=now; }
        // Remaining over the name across new passages must not auto-announce speakers.
        if(target!=null && hoverText!=null && target.Text!=hoverText) hoverText=null;
        if(hoverText!=null && decision.Text==null && (now-hoverSince).TotalMilliseconds>=250) {
            decision.Stop=true; decision.Text=hoverText; hoverText=null;
        }
        return decision;
    }
    ViewDecision UpdatePrimary(VisibleView view, bool gameFocused, DateTime now) {
        var d=new ViewDecision(); bool active=view!=null && view.Mode!="dialogue";
        bool entering=active && (!owns || mode!=view.Mode || title!=view.Title || session!=view.Session || (!focused && gameFocused));
        if(owns && (!active || mode!=view.Mode)) d.Stop=true;
        owns=active; focused=gameFocused;
        if(!active) { session=layout=selection=pending=spoken=mode=null; return d; }
        session=view.Session; mode=view.Mode; title=view.Title;
        if(mode=="opening") {
            pending=null; layout=null; selection=null;
            d.ImageIdentity=session+"|"+view.Items[0].Id; d.ImageSlot=view.Items[0].Text;
            if(d.ImageIdentity!=lastOpening) {d.Stop=true;lastOpening=d.ImageIdentity;}
            return d;
        }
        if(mode=="passage") {
            pending=null; layout=null; selection=null;
            string identity=session+"|"+view.Items[0].Id;
            if(view.Phase=="building") {
                if(entering || identity!=lastBuilding) d.Stop=true;
                lastBuilding=identity;
            } else if(identity!=lastPassage) {
                // No hover dwell or glyph idle timeout at a semantic dialogue boundary.
                d.Stop=true; d.Text=Say(view.Items[0].Text); d.IsDialogue=true; lastPassage=identity;
            }
            return d;
        }
        string nextLayout=view.LayoutKey, nextSelection="";
        ViewItem selected=null;
        foreach(var item in view.Items) if(item.Id==view.Focus) { selected=item; nextSelection=item.Id+"\t"+item.Text; break; }
        string candidate=null; bool fromHover=false;
        if(mode=="dialog" && entering) {
            var b=new StringBuilder(view.Title).Append(". ");
            foreach(var item in view.Items) b.Append(Say(item.Text)).Append(". ");
            candidate=b.ToString();
        } else if(mode=="dialog" && pending!=null && !pendingHover) {
            // Preserve the question while the pointer settles over a modal button.
        } else if(mode=="controls" && entering) {
            candidate=(String.IsNullOrWhiteSpace(view.Title)?"Controls":view.Title)+". "+(selected==null?"Point at a control to read it.":Say(selected.Text));
        } else if(mode=="links" && (entering || nextLayout!=layout)) {
            var b=new StringBuilder(view.Items.Count+" options. ");
            for(int i=0;i<view.Items.Count;i++) b.Append(i+1).Append(". ").Append(Say(view.Items[i].Text)).Append(". ");
            candidate=b.ToString();
        } else if((entering || (mode!="controls" && nextLayout!=layout) || nextSelection!=selection) && selected!=null) {
            candidate=Say(selected.Text); fromHover=true;
        } else if(entering && mode=="history") candidate="Backlog. Point at a line to read it.";
        if(entering) d.Stop=true;
        if(candidate!=null && (candidate!=pending || entering)) { pending=candidate; pendingHover=fromHover; since=now; spoken=null; }
        // Leave a button or row before the dwell completes: cancel only that hover read.
        if(pendingHover && selected==null) pending=null;
        layout=nextLayout; selection=nextSelection;
        if(gameFocused && pending!=null && pending!=spoken && (now-since).TotalMilliseconds>=150) {
            d.Text=pending; spoken=pending; pending=null;
        }
        return d;
    }
}
public sealed class StructuredViewReader {
    readonly string file; readonly Log log;
    readonly ViewNarration policy=new ViewNarration();
    VisibleView current;
    DateTime nextRead, lastValid;
    string lastSession; long lastSequence;
    public bool OwnsNarration { get { return policy.OwnsNarration; } }
    public bool HasLiveState { get { return current!=null; } }
    public string Session { get { return current==null?null:current.Session; } }
    public StructuredViewReader(string path,Log logger) { file=path; log=logger; }
    public ViewDecision Poll(bool focused, DateTime processStartUtc, DateTime now) {
        if(now>=nextRead) {
            nextRead=now.AddMilliseconds(20);
            try {
                var info=new FileInfo(file);
                if(info.Exists && info.Length<=262144 && info.LastWriteTimeUtc>=processStartUtc && (now-info.LastWriteTimeUtc).TotalSeconds<3) {
                    string text;
                    using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                    using(var reader=new StreamReader(stream,Encoding.Unicode,true)) text=reader.ReadToEnd();
                    var parsed=ViewProtocol.Parse(text);
                    if(parsed!=null && (parsed.Session!=lastSession || parsed.Sequence>lastSequence)) {
                        if(current==null || current.LayoutKey!=parsed.LayoutKey || current.Focus!=parsed.Focus || current.Phase!=parsed.Phase || (current.Hover==null?null:current.Hover.Text)!=(parsed.Hover==null?null:parsed.Hover.Text))
                            log.Write("structured-view",new { mode=parsed.Mode,title=parsed.Title,phase=parsed.Phase,focus=parsed.Focus,items=parsed.Items,hover=parsed.Hover });
                        current=parsed; lastValid=now; lastSession=parsed.Session; lastSequence=parsed.Sequence;
                    }
                }
            } catch(IOException) {} catch(UnauthorizedAccessException) {}
        }
        if((now-lastValid).TotalSeconds>3) current=null;
        return policy.Update(current,focused,now);
    }
}
}
