using System;
using System.Drawing;
using System.Windows.Forms;
namespace VisualNovelHelper {
public sealed class ShortcutDialog : Form {
    public int SelectedKey, SelectedModifiers;
    readonly ComboBox entry=new ComboBox();
    readonly Label feedback=new Label();
    readonly Action<string> announce;
    bool capturing;
    public ShortcutDialog(string action,int key,int modifiers,Action<string> speak=null) {
        announce=speak;
        Text="Shortcut: "+action; Font=new Font("Segoe UI",13); AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new Size(620,290); MinimumSize=new Size(540,300); StartPosition=FormStartPosition.CenterParent;
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=1,RowCount=5};
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label {Text="Type a shortcut, choose one, or press Record keys.\nExamples: F9 or Alt+R. Tab moves between controls.",AutoSize=true,Dock=DockStyle.Fill},0,0);
        entry.DropDownStyle=ComboBoxStyle.DropDown; entry.Dock=DockStyle.Fill; entry.AccessibleName="Shortcut, type a key combination";
        entry.Items.Add("Unassigned"); entry.Items.Add("CapsLock"); for(int i=1;i<=24;i++) entry.Items.Add("F"+i);
        for(char c='A';c<='Z';c++) entry.Items.Add(c.ToString());
        for(int i=0;i<=9;i++) entry.Items.Add("NumPad"+i);
        entry.Text=Describe(key,modifiers); layout.Controls.Add(entry,0,1);
        var actions=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill};
        var record=new Button {Text="&Record keys",AutoSize=true,MinimumSize=new Size(150,40)};
        record.Click+=delegate { capturing=true; Report("Press your shortcut now. Escape cancels recording."); };
        var clear=new Button {Text="&Unassign",AutoSize=true,MinimumSize=new Size(130,40)};
        clear.Click+=delegate { capturing=false; entry.Text="Unassigned"; Report("Shortcut unassigned. Choose Save to apply."); };
        actions.Controls.AddRange(new Control[]{record,clear}); layout.Controls.Add(actions,0,2);
        feedback.Text="Use a key your game does not use. Ctrl and Shift may trigger skipping."; feedback.Dock=DockStyle.Fill; feedback.AutoSize=true; feedback.AccessibleName="Shortcut instructions"; layout.Controls.Add(feedback,0,3);
        var buttons=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill};
        var ok=new Button {Text="&Save",AutoSize=true,MinimumSize=new Size(120,40)};
        var cancel=new Button {Text="&Cancel",AutoSize=true,MinimumSize=new Size(120,40),DialogResult=DialogResult.Cancel};
        ok.Click+=delegate {
            string error; int parsedKey,parsedModifiers;
            if(!TryParse(entry.Text,out parsedKey,out parsedModifiers,out error)) { Report(error); entry.Focus(); return; }
            SelectedKey=parsedKey; SelectedModifiers=parsedModifiers; DialogResult=DialogResult.OK; Close();
        };
        buttons.Controls.AddRange(new Control[]{ok,cancel}); layout.Controls.Add(buttons,0,4); Controls.Add(layout);
        AcceptButton=ok; CancelButton=cancel; UiAccessibility.Wire(this,()=>announce!=null,announce);
        Shown+=delegate { entry.Focus(); };
    }
    void Report(string text) { feedback.Text=text; if(announce!=null) announce(text); }
    public static void Smoke(string imagePath) {
        using(var dialog=new ShortcutDialog("Repeat",0,0)) {
            dialog.Show(); Application.DoEvents();
            dialog.entry.Text="Alt+R";
            using(var bitmap=new Bitmap(dialog.Width,dialog.Height)) { dialog.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height)); bitmap.Save(imagePath); }
            ((Button)dialog.AcceptButton).PerformClick();
            if(dialog.SelectedKey!=(int)Keys.R || dialog.SelectedModifiers!=1) throw new Exception("Typed shortcut UI smoke failed");
        }
        using(var dialog=new ShortcutDialog("Repeat",0,0)) {
            dialog.Show(); Application.DoEvents(); dialog.capturing=true;
            var message=new Message(); dialog.ProcessCmdKey(ref message,Keys.Alt|Keys.F10);
            ((Button)dialog.AcceptButton).PerformClick();
            if(dialog.SelectedKey!=(int)Keys.F10 || dialog.SelectedModifiers!=1) throw new Exception("Recorded shortcut UI smoke failed");
        }
    }
    protected override bool ProcessCmdKey(ref Message message,Keys keyData) {
        if(!capturing) return base.ProcessCmdKey(ref message,keyData);
        Keys key=keyData&Keys.KeyCode;
        if(key==Keys.Escape) { capturing=false; Report("Recording cancelled."); return true; }
        if(key==Keys.ShiftKey || key==Keys.ControlKey || key==Keys.Menu || key==Keys.LWin || key==Keys.RWin) return true;
        int mods=((keyData&Keys.Alt)!=0?1:0)|((keyData&Keys.Control)!=0?2:0)|((keyData&Keys.Shift)!=0?4:0);
        string candidate=Describe((int)key,mods),error; int parsedKey,parsedMods;
        capturing=false;
        if(TryParse(candidate,out parsedKey,out parsedMods,out error)) { entry.Text=candidate; Report("Recorded "+candidate+". Choose Save to apply."); }
        else Report(error);
        return true;
    }
    public static bool TryParse(string text,out int key,out int modifiers,out string error) {
        key=0; modifiers=0; error="Enter a key such as F9 or Alt+R, or Unassigned.";
        if(String.IsNullOrWhiteSpace(text) || text.Trim().Equals("Unassigned",StringComparison.OrdinalIgnoreCase)) { error=null; return true; }
        foreach(string part in text.Split('+')) {
            string token=part.Trim(); int mod=token.Equals("Alt",StringComparison.OrdinalIgnoreCase)?1:(token.Equals("Ctrl",StringComparison.OrdinalIgnoreCase)||token.Equals("Control",StringComparison.OrdinalIgnoreCase))?2:token.Equals("Shift",StringComparison.OrdinalIgnoreCase)?4:0;
            if(mod!=0) { if((modifiers&mod)!=0) return false; modifiers|=mod; continue; }
            if(key!=0 || token.Length==0) return false;
            Keys parsed;
            if(token.Length==1 && Char.IsDigit(token[0])) parsed=(Keys)((int)Keys.D0+(token[0]-'0'));
            else if(!Enum.TryParse<Keys>(token,true,out parsed)) return false;
            int code=(int)parsed;
            bool supported=(code>=(int)Keys.A && code<=(int)Keys.Z)||(code>=(int)Keys.D0 && code<=(int)Keys.D9)||(code>=(int)Keys.F1 && code<=(int)Keys.F24)||(code>=(int)Keys.NumPad0 && code<=(int)Keys.Divide)||parsed==Keys.Home||parsed==Keys.End||parsed==Keys.PageUp||parsed==Keys.PageDown||parsed==Keys.Insert||parsed==Keys.Delete||parsed==Keys.Pause||parsed==Keys.Scroll||parsed==Keys.Space;
            if(!supported && parsed!=Keys.CapsLock) return false; key=code;
        }
        if(key==0) { error="Include a main key, for example Alt+F9. A modifier alone cannot be assigned."; return false; }
        error=null; return true;
    }
    public static string Describe(int key,int modifiers) { if(key==0) return "Unassigned"; return ((modifiers&1)!=0?"Alt+":"")+((modifiers&2)!=0?"Ctrl+":"")+((modifiers&4)!=0?"Shift+":"")+(key==20?"CapsLock":((Keys)key).ToString()); }
}
}
