using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.Speech.Synthesis;
using System.Web.Script.Serialization;

namespace VisualNovelHelper {
public sealed class WindowsSpeech : ISpeechOutput {
    public readonly SpeechSynthesizer Engine=new SpeechSynthesizer();
    public void Speak(string text) { Engine.SpeakAsync(text); }
    public void Stop() { Engine.SpeakAsyncCancelAll(); }
    public void Dispose() { Stop(); Engine.Dispose(); }
}
public sealed class HelperWindow : Form {
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window,int id);
    readonly string root, dataRoot;
    readonly Log log;
    readonly JavaScriptSerializer json=new JavaScriptSerializer();
    readonly WindowsSpeech speech;
    readonly NarrationController narrator;
    readonly Preferences preferences;
    readonly GameProfile profile;
    readonly Label status=new Label();
    readonly Label current=new Label();
    readonly ComboBox voices=new ComboBox();
    readonly NumericUpDown rate=new NumericUpDown(), volume=new NumericUpDown();
    readonly Button toggle=new Button();
    readonly Button repeatBinding=new Button(), toggleBinding=new Button();
    readonly NotifyIcon tray=new NotifyIcon();
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    TextractorAdapter adapter;
    StructuredViewReader viewReader;
    DateTime gameStartUtc;
    bool initialized, waitingForGame;
    DateTime nextGameCheck;
    public HelperWindow(string projectRoot, bool uiSmoke=false) {
        root=projectRoot;
        dataRoot=PortablePaths.Data(root);
        Directory.CreateDirectory(Path.Combine(dataRoot,"logs"));
        Directory.CreateDirectory(Path.Combine(dataRoot,"work"));
        log=new Log(Path.Combine(dataRoot,"logs","helper-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".jsonl"));
        log.Write("startup","Creating Windows speech engine");
        speech=new WindowsSpeech();
        log.Write("startup","Windows speech engine created");
        profile=json.Deserialize<GameProfile>(File.ReadAllText(Path.Combine(root,"profiles","fata.json")));
        var prefPath=Path.Combine(dataRoot,"preferences.json");
        try { preferences=File.Exists(prefPath)?json.Deserialize<Preferences>(File.ReadAllText(prefPath)):new Preferences(); }
        catch { preferences=new Preferences(); }
        narrator=new NarrationController(speech);
        Text="VHVN — Visual Helper for Visual Novels"; Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); ClientSize=new Size(640,540); AutoScroll=true; AutoScaleDimensions=new SizeF(96,96); AutoScaleMode=AutoScaleMode.Dpi;
        Font=new Font("Segoe UI",12); StartPosition=FormStartPosition.CenterScreen;
        var heading=new Label { Text="Fata Morgana read-aloud", Font=new Font(Font.FontFamily,16,FontStyle.Bold), AutoSize=true, Location=new Point(20,16) };
        Controls.Add(heading);
        status.SetBounds(20,55,600,65); status.Text="Open the game, then connect."; status.AccessibleName="Connection status"; Controls.Add(status);
        Controls.Add(new Label { Text="Voice", Location=new Point(20,129), AutoSize=true });
        voices.SetBounds(100,124,510,30); voices.DropDownStyle=ComboBoxStyle.DropDownList; voices.AccessibleName="Reading voice";
        foreach(var voice in speech.Engine.GetInstalledVoices()) if(voice.Enabled) voices.Items.Add(voice.VoiceInfo.Name);
        if(voices.Items.Count==0) throw new InvalidOperationException("No Windows speech voices are installed.");
        voices.SelectedItem=preferences.Voice;
        if(voices.SelectedIndex<0) voices.SelectedItem=speech.Engine.Voice.Name;
        if(voices.SelectedIndex<0) voices.SelectedIndex=0;
        Controls.Add(voices);
        Controls.Add(new Label { Text="Speed", Location=new Point(20,174), AutoSize=true });
        rate.SetBounds(100,168,85,30); rate.Minimum=-10; rate.Maximum=10; rate.Value=Math.Max(-10,Math.Min(10,preferences.Rate)); rate.AccessibleName="Reading speed"; Controls.Add(rate);
        Controls.Add(new Label { Text="Volume", Location=new Point(220,174), AutoSize=true });
        volume.SetBounds(310,168,85,30); volume.Minimum=0; volume.Maximum=100; volume.Value=Math.Max(0,Math.Min(100,preferences.Volume)); volume.AccessibleName="Reading volume"; Controls.Add(volume);
        AddButton("Test voice",420,166,190,delegate { speech.Stop(); speech.Speak("Visual Novel Helper is ready to read."); });
        AddButton("Connect",20,220,135,delegate { Connect(); });
        AddButton("Repeat",170,220,135,delegate { narrator.Repeat(); });
        toggle.Text="Speech: on"; toggle.SetBounds(320,220,140,38); toggle.Click+=delegate { Toggle(); }; Controls.Add(toggle);
        AddButton("Exit helper",475,220,135,delegate { Close(); });
        repeatBinding.SetBounds(20,273,290,38); repeatBinding.Click+=delegate { ConfigureShortcut(true); }; Controls.Add(repeatBinding);
        toggleBinding.SetBounds(320,273,290,38); toggleBinding.Click+=delegate { ConfigureShortcut(false); }; Controls.Add(toggleBinding); UpdateShortcutLabels();
        current.SetBounds(20,325,600,60); current.Text="Shortcuts are optional. Choose keys unused by your game.\nMinimize this window to keep playing."; Controls.Add(current);
        Controls.Add(new Label { Text="Advance normally. Hover choices, controls, backlog lines or a speaker's printed name to hear them.", Location=new Point(20,400), Size=new Size(600,55) });
        var readControls=new CheckBox {Text="Read helper controls aloud",AccessibleName="Read helper controls aloud",Checked=preferences.ReadControls,Location=new Point(20,470),Size=new Size(590,40)};
        readControls.CheckedChanged+=delegate { preferences.ReadControls=readControls.Checked; SaveSettings(); if(preferences.ReadControls) SpeakControl("Helper control reading on."); }; Controls.Add(readControls);
        tray.Icon=Icon; tray.Text="VHVN"; tray.Visible=true;
        var menu=new ContextMenuStrip(); menu.Items.Add("Settings",null,delegate { Show(); WindowState=FormWindowState.Normal; Activate(); }); menu.Items.Add("Exit",null,delegate { Close(); }); tray.ContextMenuStrip=menu;
        tray.DoubleClick+=delegate { Show(); WindowState=FormWindowState.Normal; Activate(); };
        Resize+=delegate { if(WindowState==FormWindowState.Minimized) Hide(); };
        voices.SelectedIndexChanged+=delegate { SaveSettings(); }; rate.ValueChanged+=delegate { SaveSettings(); }; volume.ValueChanged+=delegate { SaveSettings(); };
        initialized=true; SaveSettings();
        UiAccessibility.Wire(this,()=>preferences.ReadControls && !uiSmoke,SpeakControl);
        status.TextChanged+=delegate { status.AccessibleDescription=status.Text; if(!uiSmoke && preferences.ReadControls && ContainsFocus) SpeakControl(status.Text); };
        timer.Interval=20; timer.Tick+=delegate { Tick(); }; timer.Start();
        speech.Engine.SpeakStarted+=(s,e)=>log.Write("speech-started",new { promptId=e.Prompt.GetHashCode() });
        speech.Engine.SpeakCompleted+=(s,e)=>log.Write("speech-completed",new { cancelled=e.Cancelled, error=e.Error==null?null:e.Error.Message });
        Shown+=delegate {
            log.Write("window-shown",new { smokeTest=uiSmoke });
            if(uiSmoke) {
                Application.DoEvents();
                if(voices.SelectedItem==null) throw new Exception("Voice selector has no accessible selection");
                Directory.CreateDirectory(Path.Combine(root,"tests"));
                using(var bitmap=new Bitmap(Width,Height)) { DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height)); bitmap.Save(Path.Combine(root,"tests","helper-ui.png")); }
                ShortcutDialog.Smoke(Path.Combine(root,"tests","shortcut-ui.png"));
                var closer=new System.Windows.Forms.Timer { Interval=1500 };
                closer.Tick+=delegate { closer.Stop(); closer.Dispose(); Close(); };
                closer.Start(); return;
            }
            bool a=preferences.RepeatKey==0 || RegisterHotKey(Handle,1,0x4000|(uint)preferences.RepeatModifiers,(uint)preferences.RepeatKey);
            bool b=preferences.ToggleKey==0 || RegisterHotKey(Handle,2,0x4000|(uint)preferences.ToggleModifiers,(uint)preferences.ToggleKey);
            if(!a || !b) { log.Write("hotkey-error","Shortcut already in use; use window buttons."); MessageBox.Show(this,"A shortcut is already in use. Repeat and toggle remain available in this window."); }
            Connect();
        };
        FormClosed+=delegate { timer.Stop(); UnregisterHotKey(Handle,1); UnregisterHotKey(Handle,2); narrator.Stop(); if(adapter!=null) adapter.Dispose(); tray.Dispose(); speech.Dispose(); log.Write("closed",true); log.Dispose(); };
    }
    void AddButton(string label,int x,int y,int width,Action action) { var button=new Button { Text=label, Location=new Point(x,y), Size=new Size(width,38) }; button.Click+=delegate { action(); }; Controls.Add(button); }
    void SpeakControl(string text) { speech.Stop(); speech.Speak(text); }
    void UpdateShortcutLabels() {
        repeatBinding.Text="Repeat key: "+ShortcutDialog.Describe(preferences.RepeatKey,preferences.RepeatModifiers);
        toggleBinding.Text="Toggle key: "+ShortcutDialog.Describe(preferences.ToggleKey,preferences.ToggleModifiers);
        repeatBinding.AccessibleName=repeatBinding.Text; toggleBinding.AccessibleName=toggleBinding.Text;
    }
    void ConfigureShortcut(bool repeat) {
        int key=repeat?preferences.RepeatKey:preferences.ToggleKey, modifiers=repeat?preferences.RepeatModifiers:preferences.ToggleModifiers;
        using(var dialog=new ShortcutDialog(repeat?"Repeat":"Toggle speech",key,modifiers,preferences.ReadControls?(Action<string>)SpeakControl:null)) {
            DialogResult outcome;
            UnregisterHotKey(Handle,1); UnregisterHotKey(Handle,2);
            try { outcome=dialog.ShowDialog(this); }
            finally {
                if(preferences.RepeatKey!=0) RegisterHotKey(Handle,1,0x4000|(uint)preferences.RepeatModifiers,(uint)preferences.RepeatKey);
                if(preferences.ToggleKey!=0) RegisterHotKey(Handle,2,0x4000|(uint)preferences.ToggleModifiers,(uint)preferences.ToggleKey);
            }
            if(outcome!=DialogResult.OK) return;
            int otherKey=repeat?preferences.ToggleKey:preferences.RepeatKey, otherModifiers=repeat?preferences.ToggleModifiers:preferences.RepeatModifiers;
            if(dialog.SelectedKey!=0 && dialog.SelectedKey==otherKey && dialog.SelectedModifiers==otherModifiers) { MessageBox.Show(this,"Choose a different shortcut for each action."); return; }
            int id=repeat?1:2; UnregisterHotKey(Handle,id);
            if(dialog.SelectedKey!=0 && !RegisterHotKey(Handle,id,0x4000|(uint)dialog.SelectedModifiers,(uint)dialog.SelectedKey)) {
                if(key!=0) RegisterHotKey(Handle,id,0x4000|(uint)modifiers,(uint)key);
                MessageBox.Show(this,"That shortcut is already in use. Your previous setting has been kept."); return;
            }
            if(repeat) { preferences.RepeatKey=dialog.SelectedKey; preferences.RepeatModifiers=dialog.SelectedModifiers; }
            else { preferences.ToggleKey=dialog.SelectedKey; preferences.ToggleModifiers=dialog.SelectedModifiers; }
            UpdateShortcutLabels(); SaveSettings(); log.Write("shortcut-changed",new { action=repeat?"repeat":"toggle",key=dialog.SelectedKey,modifiers=dialog.SelectedModifiers });
        }
    }
    void SaveSettings() {
        if(!initialized) return;
        preferences.Voice=(string)voices.SelectedItem; preferences.Rate=(int)rate.Value; preferences.Volume=(int)volume.Value;
        speech.Stop(); speech.Engine.SelectVoice(preferences.Voice); speech.Engine.Rate=preferences.Rate; speech.Engine.Volume=preferences.Volume;
        File.WriteAllText(Path.Combine(dataRoot,"preferences.json"),json.Serialize(preferences));
    }
    void Toggle() { narrator.Toggle(); toggle.Text=narrator.Enabled?"Speech: on":"Speech: off"; toggle.AccessibleName=toggle.Text; log.Write("speech-enabled",narrator.Enabled); }
    bool GameFocused() { uint pid; GetWindowThreadProcessId(GetForegroundWindow(),out pid); return adapter!=null && pid==adapter.GamePid; }
    void Connect() {
        try {
            narrator.Stop(); if(adapter!=null) adapter.Dispose();
            adapter=null; viewReader=null; waitingForGame=false;
            if(System.Diagnostics.Process.GetProcessesByName(profile.processName).Length==0) {
                waitingForGame=true; status.Text="Ready. Start Fata Morgana through Steam. I will connect automatically."; return;
            }
            adapter=new TextractorAdapter(root,profile,log);
            adapter.Status+=s=> { status.Text=s; };
            adapter.DialogueReceived+=d=> {
                bool suppressed=viewReader!=null && viewReader.OwnsNarration;
                if(!suppressed) narrator.Receive(d);
                log.Write("narration-decision",new { enabled=narrator.Enabled, gameFocused=narrator.Focused, structuredViewSuppressed=suppressed });
            };
            adapter.Start();
            using(var game=System.Diagnostics.Process.GetProcessById(adapter.GamePid)) gameStartUtc=game.StartTime.ToUniversalTime();
            viewReader=new StructuredViewReader(Path.Combine(dataRoot,"work","bridge-state.txt"),log);
        } catch(Exception ex) { Fail(ex); }
    }
    void Fail(Exception ex) { status.Text=ex.Message; log.Write("error",ex.ToString()); narrator.Stop(); if(adapter!=null) { adapter.Dispose(); adapter=null; } Show(); WindowState=FormWindowState.Normal; }
    void Tick() {
        try {
            if(waitingForGame && DateTime.UtcNow>=nextGameCheck) {
                nextGameCheck=DateTime.UtcNow.AddSeconds(1);
                if(System.Diagnostics.Process.GetProcessesByName(profile.processName).Length>0) Connect();
            }
            bool focused=GameFocused(); narrator.SetFocus(focused);
            if(adapter!=null) {
                if(viewReader!=null) {
                    var decision=viewReader.Poll(focused,gameStartUtc,DateTime.UtcNow);
                    if(decision.Stop) narrator.Stop();
                    if(decision.Text!=null) {
                        narrator.Receive(new Dialogue { Text=decision.Text, Source="engine-visible-state" });
                        log.Write("structured-narration",decision.Text);
                    }
                }
                adapter.Poll();
            }
        } catch(Exception ex) { Fail(ex); }
    }
    protected override void WndProc(ref Message m) {
        if(m.Msg==0x312 && GameFocused()) { if(m.WParam.ToInt32()==1) { narrator.Repeat(); log.Write("repeat",true); } if(m.WParam.ToInt32()==2) Toggle(); }
        base.WndProc(ref m);
    }
}
public static class Program {
    [STAThread] public static int Main(string[] args) {
        var root=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".."));
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length>0 && args[0]=="--setup") return PortableSetup.Run(root,false);
        if(args.Length>0 && args[0]=="--uninstall") return PortableSetup.Run(root,true);
        if(args.Length>0 && args[0]=="--remove-installed-bridge") return PortableSetup.RemoveInstalled(root);
        if(args.Length==5 && args[0]=="--setup-worker") return PortableSetup.Worker(root,args);
        if(args.Length==3 && args[0]=="--portable-test") return PortableSelfTests.Run(root,args[1],args[2]);
        var dataRoot=PortablePaths.Data(root); Directory.CreateDirectory(dataRoot);
        var startupLog=Path.Combine(dataRoot,"startup-diagnostics.log");
        Action<string> trace=s=>File.AppendAllText(startupLog,DateTime.UtcNow.ToString("o")+" pid="+System.Diagnostics.Process.GetCurrentProcess().Id+" "+s+Environment.NewLine);
        trace("Starting "+(args.Length>0?args[0]:"reader"));
        AppDomain.CurrentDomain.UnhandledException+=(s,e)=>trace("Unhandled: "+e.ExceptionObject);
        if(args.Length>1 && args[0]=="--ocr-test") {
            try { var lines=ScreenReader.RecognizeFile(args[1]).GetAwaiter().GetResult(); File.WriteAllText(Path.Combine(root,"tests","ocr-result.json"),new JavaScriptSerializer().Serialize(lines)); return 0; }
            catch(Exception ex) { trace(ex.ToString()); return 1; }
        }
        if(args.Length>0 && args[0]=="--self-test") return SelfTests.Run(root,args.Length>1?args[1]:null);
        if(args.Length>0 && args[0]=="--speech-smoke") {
            trace("Creating speech engine");
            using(var output=new WindowsSpeech()) { trace("Engine created"); output.Engine.SetOutputToWaveFile(Path.Combine(root,"tests","speech-smoke.wav")); output.Engine.Speak("Visual Novel Helper speech test."); trace("WAV generated"); } trace("Speech engine disposed"); return 0;
        }
        bool created;
        using(var mutex=new Mutex(true,"Local\\VisualNovelHelper",out created)) {
            if(!created) { MessageBox.Show("Visual Novel Helper is already running. Open it from the notification area."); return 1; }
            try {
                if(args.Length==0 && File.Exists(Path.Combine(root,"portable.txt")) && PortableSetup.NeedsSetup(root,dataRoot)) {
                    if(PortableSetup.Run(root,false)!=0) return 1;
                }
                Application.Run(new HelperWindow(root,args.Length>0 && args[0]=="--ui-smoke")); trace("Exited normally"); return 0;
            }
            catch(Exception ex) { File.WriteAllText(Path.Combine(dataRoot,"startup-error.log"),ex.ToString()); MessageBox.Show(ex.Message,"Visual Novel Helper"); return 1; }
            finally { mutex.ReleaseMutex(); }
        }
    }
}
}
