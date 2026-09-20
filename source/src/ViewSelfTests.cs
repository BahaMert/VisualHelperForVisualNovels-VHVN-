using System;
using System.IO;
using System.Text;
namespace VisualNovelHelper {
public static class ViewSelfTests {
    static void Check(bool ok,string name) { if(!ok) throw new Exception("FAIL: structured view "+name); }
    static string Frame(string body,int seq=1) {
        return "VNH2\t123-456\t"+seq+"\n"+body+"\nEND\t123-456\t"+seq+"\t"+body.Length+"\t"+ViewProtocol.Checksum(body)+"\n";
    }
    public static void Run(string root) {
        string body="mode\tlinks\nitem\tc:0:0\tHello%25%09world\nitem\tc:0:1\t............\nfocus\t";
        string frame=Frame(body);
        var view=ViewProtocol.Parse(frame);
        Check(view!=null && view.Items[0].Text=="Hello%\tworld","frame and one-pass escapes");
        Check(ViewProtocol.Parse(frame.Replace("Hello","Jello"))==null,"torn body rejected by checksum");
        Check(ViewProtocol.Parse(frame.Substring(0,frame.Length-12))==null,"partial footer rejected");
        Check(ViewProtocol.Parse(Frame(body.Replace("c:0:1","c:0:0")))==null,"duplicate IDs rejected");
        Check(ViewProtocol.Parse(Frame(body+"unknown\tvalue"))==null,"unknown records rejected");
        Check(ViewProtocol.Parse(Frame(body+"c:9:9"))==null,"focus must exist");
        var fixture=Path.Combine(root,"work","engine-probe","bridge-test.txt");
        Check(File.Exists(fixture) && ViewProtocol.Parse(File.ReadAllText(fixture))!=null,"actual TJS checksum compatibility");
        var now=new DateTime(2026,9,19,0,0,0,DateTimeKind.Utc);
        var p=new ViewNarration();
        Check(p.Update(view,true,now).Text==null && p.OwnsNarration,"initial dwell and source priority");
        string spoken=p.Update(view,true,now.AddMilliseconds(160)).Text;
        Check(spoken!=null && spoken.Contains("2 options") && spoken.Contains("Silence"),"options announced separately including silence");
        Check(p.Update(view,true,now.AddSeconds(1)).Text==null,"stationary view not repeated");
        view.Focus="c:0:0"; p.Update(view,true,now.AddSeconds(2));
        Check(p.Update(view,true,now.AddSeconds(2.2)).Text=="Hello%\tworld","focused option reads");
        view.Focus=""; p.Update(view,true,now.AddSeconds(3));
        view.Focus="c:0:0"; p.Update(view,true,now.AddSeconds(4));
        Check(p.Update(view,true,now.AddSeconds(4.2)).Text!=null,"revisiting option repeats");
        view.Focus="c:0:1"; p.Update(view,true,now.AddSeconds(4.3));
        view.Focus=""; p.Update(view,true,now.AddSeconds(4.35));
        Check(p.Update(view,true,now.AddSeconds(4.6)).Text==null,"leaving a menu button cancels pending hover speech");
        var history=ViewProtocol.Parse(Frame("mode\thistory\nitem\th:0:0\tFirst line\nitem\th:0:1\tSecond line\nfocus\th:0:0"));
        Check(p.Update(history,true,now.AddSeconds(5)).Stop,"view transition stops old speech");
        history.Focus="h:0:1"; p.Update(history,true,now.AddSeconds(5.1));
        Check(p.Update(history,true,now.AddSeconds(5.2)).Text==null,"hover dwell restarts");
        Check(p.Update(history,true,now.AddSeconds(5.3)).Text=="Second line","backlog reads hovered line");
        history.Focus="h:0:0"; p.Update(history,true,now.AddSeconds(6));
        history.Focus=""; p.Update(history,true,now.AddSeconds(6.05));
        Check(p.Update(history,true,now.AddSeconds(6.4)).Text==null,"leaving row cancels pending read");
        Check(p.Update(null,true,now.AddSeconds(7)).Stop && !p.OwnsNarration,"stale view returns dialogue ownership");
        p.Update(view,false,now.AddSeconds(8));
        Check(p.Update(view,false,now.AddSeconds(9)).Text==null,"background view silent");
        p.Update(view,true,now.AddSeconds(10));
        Check(p.Update(view,true,now.AddSeconds(10.2)).Text!=null,"visible view announced on focus return");
        var control=ViewProtocol.Parse(Frame("mode\tcontrols\ntitle\tSettings\nitem\tvolume\tMusic volume: 30 percent\nitem\tspeed\tText speed: 20 milliseconds\nfocus\tvolume"));
        Check(control!=null,"named controls parse");
        p.Update(control,true,now.AddSeconds(11));
        var intro=p.Update(control,true,now.AddSeconds(11.2)).Text;
        Check(intro=="Settings. Music volume: 30 percent","control screen introduction only reads current control");
        control.Items[1].Text="Text speed: 10 milliseconds";
        Check(p.Update(control,true,now.AddSeconds(12)).Text==null,"unfocused value changes stay quiet");
        control.Items[0].Text="Music volume: 45 percent"; p.Update(control,true,now.AddSeconds(13));
        Check(p.Update(control,true,now.AddSeconds(13.2)).Text=="Music volume: 45 percent","focused live value read without entire screen replay");
        var building=ViewProtocol.Parse(Frame("mode\tpassage\nphase\tbuilding\nitem\tp:1\t\nfocus\t"));
        var ready=ViewProtocol.Parse(Frame("mode\tpassage\nphase\tready\nitem\tp:1\tSame words\nfocus\t"));
        Check(building!=null && ready!=null,"semantic passage frames");
        Check(p.Update(building,true,now.AddSeconds(14)).Stop && p.OwnsNarration,"new text stops speech and suppresses hook fragments");
        Check(p.Update(ready,true,now.AddSeconds(14.01)).Text=="Same words","ready passage reads without hover or hook timeout");
        Check(p.Update(ready,true,now.AddSeconds(15)).Text==null,"ready heartbeat does not repeat");
        p.Update(ready,false,now.AddSeconds(16));
        Check(p.Update(ready,true,now.AddSeconds(17)).Text==null,"returning focus does not replay a passage");
        ready.Items[0].Id="p:2";
        Check(p.Update(ready,true,now.AddSeconds(18)).Text=="Same words","identical next passage still reads");
        p.Update(history,true,now.AddSeconds(19));
        Check(p.Update(ready,true,now.AddSeconds(20)).Text==null,"closing backlog does not replay passage");
        Check(ViewProtocol.Parse(Frame("mode\tpassage\nphase\tbuilding\nitem\tp:3\tPartial text\nfocus\t"))==null,"building frame cannot leak partial text");
        var confirm=ViewProtocol.Parse(Frame("mode\tdialog\ntitle\tReturn to title? Unsaved progress will be lost\nitem\tconfirm:0\tYes\nitem\tconfirm:1\tNo\nfocus\t"));
        Check(confirm!=null,"confirmation dialog parse");
        p.Update(confirm,true,now.AddSeconds(21));
        confirm.Focus="confirm:0"; p.Update(confirm,true,now.AddSeconds(21.05));
        Check(p.Update(confirm,true,now.AddSeconds(21.2)).Text=="Return to title? Unsaved progress will be lost. Yes. No. ","confirmation question and both options announced");
        Check(p.Update(confirm,true,now.AddSeconds(22)).Text==null,"stationary confirmation stays quiet");
        confirm.Focus="confirm:1"; p.Update(confirm,true,now.AddSeconds(23));
        Check(p.Update(confirm,true,now.AddSeconds(23.2)).Text=="No","confirmation hover reads option");
        p.Update(ready,true,now.AddSeconds(24));
        Check(p.Update(ready,true,now.AddSeconds(24.2)).Text==null,"closing confirmation does not replay dialogue");
        ready.Hover=new ViewItem {Id="speaker:0",Text="Test Speaker"};
        Check(p.Update(ready,true,now.AddSeconds(25)).Text==null,"speaker waits for intentional hover dwell");
        Check(p.Update(ready,true,now.AddSeconds(25.3)).Text=="Test Speaker","speaker name spoken only on hover");
        Check(p.Update(ready,true,now.AddSeconds(26)).Text==null,"stationary speaker not repeated");
        ready.Items[0].Id="p:3"; ready.Hover.Text="New Speaker";
        Check(p.Update(ready,true,now.AddSeconds(27)).Text=="Same words","dialogue advance still reads without automatic name");
        Check(p.Update(ready,true,now.AddSeconds(27.3)).Text==null,"stationary pointer does not read changed speaker");
        ready.Hover=null; p.Update(ready,true,now.AddSeconds(28));
        ready.Hover=new ViewItem {Id="speaker:0",Text="New Speaker"}; p.Update(ready,true,now.AddSeconds(29));
        Check(p.Update(ready,true,now.AddSeconds(29.3)).Text=="New Speaker","leaving and reentering reads name again");
        ready.Hover=null; p.Update(ready,true,now.AddSeconds(30));
        ready.Hover=new ViewItem {Id="speaker:0",Text="New Speaker"}; p.Update(ready,true,now.AddSeconds(31));
        ready.Hover=null; p.Update(ready,true,now.AddSeconds(31.1));
        Check(p.Update(ready,true,now.AddSeconds(31.5)).Text==null,"brief crossing cancels name");
        Check(ViewProtocol.Parse(Frame("mode\tdialogue\nhover\tspeaker:0\tSomeone\nfocus\t")).Hover.Text=="Someone","generic optional hover protocol");
        int hotkey,mods; string error;
        Check(ShortcutDialog.TryParse("alt + r",out hotkey,out mods,out error) && hotkey==(int)System.Windows.Forms.Keys.R && mods==1,"typed case-insensitive shortcut");
        Check(ShortcutDialog.TryParse("F24",out hotkey,out mods,out error) && mods==0,"unmodified typed function key");
        Check(!ShortcutDialog.TryParse("Ctrl",out hotkey,out mods,out error),"modifier-only rejected");
        Check(!ShortcutDialog.TryParse("Alt+R+T",out hotkey,out mods,out error),"multiple main keys rejected");
        Check(!ShortcutDialog.TryParse("Ctrl+Ctrl+F9",out hotkey,out mods,out error),"duplicate modifiers rejected");
        Check(ShortcutDialog.TryParse("Unassigned",out hotkey,out mods,out error) && hotkey==0,"typed unassign");
        // File reader cannot replay a prior game's snapshot and expires stopped heartbeats.
        var path=Path.Combine(root,"tests","view-reader-fixture.txt");
        File.WriteAllText(path,frame,Encoding.Unicode); File.SetLastWriteTimeUtc(path,now);
        using(var log=new Log(Path.Combine(root,"tests","view-reader-test.jsonl"))) {
            var reader=new StructuredViewReader(path,log);
            reader.Poll(true,now.AddSeconds(1),now.AddSeconds(1));
            Check(!reader.OwnsNarration,"previous-process file rejected");
            reader.Poll(true,now.AddSeconds(-1),now.AddSeconds(1.1));
            Check(reader.OwnsNarration,"fresh current-process file accepted");
            reader.Poll(true,now.AddSeconds(-1),now.AddSeconds(5));
            Check(!reader.OwnsNarration,"dead bridge expires");
        }
    }
}
}
