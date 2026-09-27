using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace VisualNovelHelper {
public sealed class VoiceSetupDialog : Form {
    public VoiceSetupDialog(Action<string> speak) {
        Text="Windows natural voices"; ClientSize=new Size(660,540);
        Font=new Font("Segoe UI",12); StartPosition=FormStartPosition.CenterParent;
        MinimizeBox=false; MaximizeBox=false; FormBorderStyle=FormBorderStyle.FixedDialog;
        var instructions=new TextBox { Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical,
            AccessibleName="Natural voice setup instructions", Location=new Point(20,20), Size=new Size(620,400),
            Text="VHVN lists voices provided by Windows SAPI, including compatible natural voice adapters.\r\n\r\n"+
            "For Windows Narrator natural voices, a separate 64-bit NaturalVoiceSAPIAdapter is required. Installing a voice in Narrator alone does not make it available here.\r\n\r\n"+
            "1. Open the adapter guide and read its current compatibility notes. Newer Narrator voice packs may not work; the guide links compatible versions.\r\n"+
            "2. Extract the compatible voice MSIX into a separate folder. Set Local voice path in the adapter to that folder. Keep your existing Magnifier/Narrator voice installed.\r\n"+
            "3. Install the adapter's 64-bit component for VHVN; add its 32-bit component for other 32-bit SAPI apps.\r\n"+
            "4. For offline reading, disable Microsoft Edge online voices and Azure online voices in the adapter. Online voices send text to their speech service.\r\n"+
            "5. Return to VHVN, choose Refresh voices, select a voice and use Test voice. Restart VHVN if a newly installed voice is still missing.\r\n\r\n"+
            "The adapter is an optional third-party component, not bundled with VHVN. Compatibility depends on the voice package and adapter. Existing Windows desktop voices remain available." };
        Controls.Add(instructions);
        var guide=new Button { Text="Open adapter guide", Location=new Point(20,435),Size=new Size(300,40) };
        guide.Click+=delegate { Open("https://github.com/gexgd0419/NaturalVoiceSAPIAdapter"); }; Controls.Add(guide);
        var settings=new Button { Text="Windows speech settings", Location=new Point(340,435),Size=new Size(300,40) };
        settings.Click+=delegate { Open("ms-settings:speech"); }; Controls.Add(settings);
        var close=new Button {Text="Close",DialogResult=DialogResult.Cancel,Location=new Point(490,490),Size=new Size(150,36)};
        Controls.Add(close); CancelButton=close;
        UiAccessibility.Wire(this,()=>speak!=null,speak);
        // The main helper normally reads labels, not textbox contents.
        instructions.Enter+=delegate { if(speak!=null) speak(instructions.Text); };
        Shown+=delegate { instructions.Select(0,0); };
    }
    void Open(string target) {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute=true }); }
        catch(Exception ex) { MessageBox.Show(this,"Could not open the setup page. "+ex.Message,Text); }
    }
}
}
