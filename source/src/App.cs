using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;

namespace VisualNovelHelper {
public sealed class HelperWindow : Form {
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
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
    readonly Label voiceStatus=new Label();
    bool loadingVoices, preserveMissingVoice;
    readonly NumericUpDown rate=new NumericUpDown(), volume=new NumericUpDown();
    readonly Button toggle=new Button();
    readonly Button repeatBinding=new Button(), toggleBinding=new Button(), skipBinding=new Button();
    readonly GameInputChannel gameInput;
    OpeningReader openingReader;
    readonly NotifyIcon tray=new NotifyIcon();
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    TextractorAdapter adapter;
    StructuredViewReader viewReader;
    GameShortcuts shortcuts;
    DateTime gameStartUtc;
    bool initialized, waitingForGame;
    DateTime nextGameCheck;
    public HelperWindow(string projectRoot, bool uiSmoke=false) {
        root=projectRoot;
        dataRoot=PortablePaths.Data(root);
        gameInput=new GameInputChannel(Path.Combine(dataRoot,"work","bridge-state.txt"));
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
        Text="VHVN — Visual Helper for Visual Novels"; Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); ClientSize=new Size(640,775); AutoScroll=true; AutoScaleDimensions=new SizeF(96,96); AutoScaleMode=AutoScaleMode.Dpi;
        Font=new Font("Segoe UI",12); StartPosition=FormStartPosition.CenterScreen;
        var heading=new Label { Text="Fata Morgana read-aloud", Font=new Font(Font.FontFamily,16,FontStyle.Bold), AutoSize=true, Location=new Point(20,16) };
        Controls.Add(heading);
        status.SetBounds(20,55,600,65); status.Text="Open the game, then connect."; status.AccessibleName="Connection status"; Controls.Add(status);
        Controls.Add(new Label { Text="Voice", Location=new Point(20,129), AutoSize=true });
        voices.SetBounds(100,124,510,30); voices.DropDownStyle=ComboBoxStyle.DropDownList; voices.AccessibleName="Reading voice";
        LoadVoices();
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
        repeatBinding.SetBounds(20,273,290,38); repeatBinding.Click+=delegate { ConfigureShortcut(0); }; Controls.Add(repeatBinding);
        toggleBinding.SetBounds(320,273,290,38); toggleBinding.Click+=delegate { ConfigureShortcut(1); }; Controls.Add(toggleBinding);
        skipBinding.SetBounds(20,320,590,38); skipBinding.Click+=delegate { ConfigureShortcut(2); }; Controls.Add(skipBinding); UpdateShortcutLabels();
        var remap=new CheckBox {Text="Free Ctrl for Magnifier; use the hold-to-skip key",AccessibleName="Free Ctrl for Magnifier",Checked=preferences.RemapSkip,Location=new Point(20,365),Size=new Size(595,38)};
        remap.CheckedChanged+=delegate { preferences.RemapSkip=remap.Checked; if(shortcuts!=null) shortcuts.Configure(preferences); SaveSettings(); }; Controls.Add(remap);
        current.SetBounds(20,411,600,55); current.Text="Hold the skip key to fast-forward; release it to stop.\nKeys work normally outside the game."; Controls.Add(current);
        Controls.Add(new Label { Text="Advance normally. Hover choices, controls, backlog lines or a speaker's printed name to hear them.", Location=new Point(20,479), Size=new Size(600,55) });
        var opening=new CheckBox {Text="Read opening image cards (Windows OCR)",AccessibleName="Read opening image cards",Checked=preferences.ReadOpening,Location=new Point(20,543),Size=new Size(590,40)};
        opening.CheckedChanged+=delegate {preferences.ReadOpening=opening.Checked; SaveSettings();}; Controls.Add(opening);
        var readControls=new CheckBox {Text="Read helper controls aloud",AccessibleName="Read helper controls aloud",Checked=preferences.ReadControls,Location=new Point(20,591),Size=new Size(590,40)};
        readControls.CheckedChanged+=delegate { preferences.ReadControls=readControls.Checked; SaveSettings(); if(preferences.ReadControls) SpeakControl("Helper control reading on."); }; Controls.Add(readControls);
        AddButton("Natural voices / setup",20,640,290,delegate {
            using(var dialog=new VoiceSetupDialog(preferences.ReadControls?(Action<string>)SpeakControl:null)) dialog.ShowDialog(this);
        });
        AddButton("Refresh voices",320,640,290,delegate {
            try { speech.Refresh(); LoadVoices(); SaveSettings(); }
            catch(Exception ex) { VoiceFailed(ex.Message); }
        });
        voiceStatus.SetBounds(20,686,600,80); voiceStatus.AccessibleName="Voice status"; Controls.Add(voiceStatus);
        speech.Failed+=VoiceFailed;
        speech.Diagnostic+=kind=>log.Write(kind,new { voiceId=speech.SelectedId });
        tray.Icon=Icon; tray.Text="VHVN"; tray.Visible=true;
        var menu=new ContextMenuStrip(); menu.Items.Add("Settings",null,delegate { Show(); WindowState=FormWindowState.Normal; Activate(); }); menu.Items.Add("Exit",null,delegate { Close(); }); tray.ContextMenuStrip=menu;
        tray.DoubleClick+=delegate { Show(); WindowState=FormWindowState.Normal; Activate(); };
        Resize+=delegate { if(WindowState==FormWindowState.Minimized) Hide(); };
        voices.SelectedIndexChanged+=delegate { if(!loadingVoices) { preserveMissingVoice=false; voiceStatus.Text=""; SaveSettings(); } }; rate.ValueChanged+=delegate { SaveSettings(); }; volume.ValueChanged+=delegate { SaveSettings(); };
        initialized=true; SaveSettings();
        UiAccessibility.Wire(this,()=>preferences.ReadControls && !uiSmoke,SpeakControl);
        status.TextChanged+=delegate { status.AccessibleDescription=status.Text; if(!uiSmoke && preferences.ReadControls && ContainsFocus) SpeakControl(status.Text); };
        timer.Interval=20; timer.Tick+=delegate { Tick(); }; timer.Start();
        Shown+=delegate {
            log.Write("window-shown",new { smokeTest=uiSmoke });
            if(uiSmoke) {
                Application.DoEvents();
                if(voices.SelectedItem==null) throw new Exception("Voice selector has no accessible selection");
                if(String.IsNullOrWhiteSpace(voices.Text)) throw new Exception("Selected voice has no visible label");
                Directory.CreateDirectory(Path.Combine(root,"tests"));
                using(var bitmap=new Bitmap(Width,Height)) { DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height)); bitmap.Save(Path.Combine(root,"tests","helper-ui.png")); }
                ShortcutDialog.Smoke(Path.Combine(root,"tests","shortcut-ui.png"));
                PortableSetup.ConfirmationSmoke(Path.Combine(root,"tests","replacement-ui.png"));
                using(var dialog=new VoiceSetupDialog(null)) {
                    dialog.Show(); Application.DoEvents();
                    using(var bitmap=new Bitmap(dialog.Width,dialog.Height)) { dialog.DrawToBitmap(bitmap,new Rectangle(0,0,dialog.Width,dialog.Height)); bitmap.Save(Path.Combine(root,"tests","voice-setup-ui.png")); }
                    dialog.Close();
                }
                var closer=new System.Windows.Forms.Timer { Interval=1500 };
                closer.Tick+=delegate { closer.Stop(); closer.Dispose(); Close(); };
                closer.Start(); return;
            }
            try { shortcuts=new GameShortcuts(Handle); shortcuts.Configure(preferences); }
            catch(Exception ex) { log.Write("shortcut-error",ex.Message); MessageBox.Show(this,"Game shortcuts could not be enabled. The Repeat and Speech buttons still work."); }
            Connect();
        };
        FormClosed+=delegate { timer.Stop(); ReleaseGameInput(); if(shortcuts!=null) shortcuts.Dispose(); narrator.Stop(); if(adapter!=null) adapter.Dispose(); tray.Dispose(); speech.Dispose(); log.Write("closed",true); log.Dispose(); };
    }
    void AddButton(string label,int x,int y,int width,Action action) { var button=new Button { Text=label, Location=new Point(x,y), Size=new Size(width,38) }; button.Click+=delegate { action(); }; Controls.Add(button); }
    void SpeakControl(string text) { speech.Stop(); speech.Speak(text); }
    void UpdateShortcutLabels() {
        repeatBinding.Text="Repeat key: "+ShortcutDialog.Describe(preferences.RepeatKey,preferences.RepeatModifiers);
        toggleBinding.Text="Toggle key: "+ShortcutDialog.Describe(preferences.ToggleKey,preferences.ToggleModifiers);
        skipBinding.Text="Hold-to-skip key: "+ShortcutDialog.Describe(preferences.SkipKey,preferences.SkipModifiers);
        skipBinding.AccessibleName=skipBinding.Text;
        repeatBinding.AccessibleName=repeatBinding.Text; toggleBinding.AccessibleName=toggleBinding.Text;
    }
    void ConfigureShortcut(int action) {
        int key=action==0?preferences.RepeatKey:action==1?preferences.ToggleKey:preferences.SkipKey, modifiers=action==0?preferences.RepeatModifiers:action==1?preferences.ToggleModifiers:preferences.SkipModifiers;
        using(var dialog=new ShortcutDialog(action==0?"Repeat":action==1?"Toggle speech":"Hold to skip",key,modifiers,preferences.ReadControls?(Action<string>)SpeakControl:null)) {
            DialogResult outcome;
            if(shortcuts!=null) shortcuts.Suspended=true;
            try { outcome=dialog.ShowDialog(this); }
            finally { if(shortcuts!=null) shortcuts.Suspended=false; }
            if(outcome!=DialogResult.OK) return;
            int[] keys={preferences.RepeatKey,preferences.ToggleKey,preferences.SkipKey}, mods={preferences.RepeatModifiers,preferences.ToggleModifiers,preferences.SkipModifiers};
            for(int i=0;i<3;i++) if(i!=action && dialog.SelectedKey!=0 && dialog.SelectedKey==keys[i] && dialog.SelectedModifiers==mods[i]) { MessageBox.Show(this,"Choose a different shortcut for each action."); return; }
            if(action==0) { preferences.RepeatKey=dialog.SelectedKey; preferences.RepeatModifiers=dialog.SelectedModifiers; }
            else if(action==1) { preferences.ToggleKey=dialog.SelectedKey; preferences.ToggleModifiers=dialog.SelectedModifiers; }
            else { preferences.SkipKey=dialog.SelectedKey; preferences.SkipModifiers=dialog.SelectedModifiers; }
            if(shortcuts!=null) shortcuts.Configure(preferences);
            UpdateShortcutLabels(); SaveSettings(); log.Write("shortcut-changed",new { action=action,key=dialog.SelectedKey,modifiers=dialog.SelectedModifiers });
        }
    }
    void SaveSettings() {
        if(!initialized || loadingVoices) return;
        var selected=voices.SelectedItem as SpeechVoice;
        string previous=speech.SelectedId;
        try { speech.Configure(selected,(int)rate.Value,(int)volume.Value); }
        catch(Exception ex) {
            loadingVoices=true;
            try { voices.SelectedItem=WindowsSpeech.Resolve(speech.Voices,previous,null); }
            finally { loadingVoices=false; }
            VoiceFailed(ex.Message); return;
        }
        if(!preserveMissingVoice) { preferences.Voice=selected.Name; preferences.VoiceId=selected.Id; }
        preferences.Rate=(int)rate.Value; preferences.Volume=(int)volume.Value;
        File.WriteAllText(Path.Combine(dataRoot,"preferences.json"),json.Serialize(preferences));
    }
    void LoadVoices() {
        loadingVoices=true;
        try {
            voices.Items.Clear(); foreach(var voice in speech.Voices) voices.Items.Add(voice);
            var saved=WindowsSpeech.Resolve(speech.Voices,preferences.VoiceId,preferences.Voice);
            preserveMissingVoice=saved==null && (!String.IsNullOrEmpty(preferences.VoiceId) || !String.IsNullOrEmpty(preferences.Voice));
            voices.SelectedItem=saved ?? speech.DefaultVoice;
            bool hasNatural=false;
            foreach(var voice in speech.Voices) if(voice.Description.IndexOf("(Natural)",StringComparison.OrdinalIgnoreCase)>=0) hasNatural=true;
            voiceStatus.Text=preserveMissingVoice?"Saved voice unavailable. Using "+voices.SelectedItem+" for now. Refresh after installing the voice, or select a replacement.":speech.Voices.Count+" Windows voices available. "+(hasNatural?"Natural voices are available in the voice list.":"Natural voices may need additional setup.");
        } finally { loadingVoices=false; }
    }
    void VoiceFailed(string message) { voiceStatus.Text=message; voiceStatus.AccessibleDescription=message; log.Write("speech-error",message); }
    void Toggle() { narrator.Toggle(); toggle.Text=narrator.Enabled?"Speech: on":"Speech: off"; toggle.AccessibleName=toggle.Text; log.Write("speech-enabled",narrator.Enabled); }
    bool GameFocused() { uint pid; GetWindowThreadProcessId(GetForegroundWindow(),out pid); return adapter!=null && pid==adapter.GamePid; }
    void ReleaseGameInput() { try { gameInput.Publish(viewReader==null?null:viewReader.Session,false,0,false,DateTime.UtcNow); } catch(Exception ex) {log.Write("input-release-error",ex.Message);} }
    void Connect() {
        try {
            ReleaseGameInput(); narrator.Stop(); narrator.ClearDialogue(); if(shortcuts!=null) shortcuts.GamePid=0; if(adapter!=null) adapter.Dispose();
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
            if(shortcuts!=null) shortcuts.GamePid=adapter.GamePid;
            using(var game=System.Diagnostics.Process.GetProcessById(adapter.GamePid)) gameStartUtc=game.StartTime.ToUniversalTime();
            viewReader=new StructuredViewReader(Path.Combine(dataRoot,"work","bridge-state.txt"),log);
            openingReader=new OpeningReader(slot=>System.Threading.Tasks.Task.Run(()=>OpeningReader.Recognize(Path.Combine(dataRoot,"work","bridge-state.txt"),slot)));
        } catch(Exception ex) { Fail(ex); }
    }
    void Fail(Exception ex) { ReleaseGameInput(); status.Text=ex.Message; log.Write("error",ex.ToString()); narrator.Stop(); if(shortcuts!=null) shortcuts.GamePid=0; if(adapter!=null) { adapter.Dispose(); adapter=null; } Show(); WindowState=FormWindowState.Normal; }
    void Tick() {
        try {
            speech.Poll();
            if(waitingForGame && DateTime.UtcNow>=nextGameCheck) {
                nextGameCheck=DateTime.UtcNow.AddSeconds(1);
                if(System.Diagnostics.Process.GetProcessesByName(profile.processName).Length>0) Connect();
            }
            bool focused=GameFocused(); narrator.SetFocus(focused);
            if(!focused && shortcuts!=null) shortcuts.ReleaseSkip();
            if(adapter!=null) {
                if(viewReader!=null) {
                    var decision=viewReader.Poll(focused,gameStartUtc,DateTime.UtcNow);
                    adapter.ObserveBridge(viewReader.HasLiveState,DateTime.UtcNow);
                    gameInput.Publish(viewReader.Session,preferences.RemapSkip && shortcuts!=null,preferences.SkipKey,focused && shortcuts!=null && shortcuts.SkipHeld,DateTime.UtcNow);
                    if(decision.Stop && decision.Text==null) narrator.Stop();
                    if(decision.Text!=null) {
                        narrator.Receive(new Dialogue { Text=decision.Text, Source="engine-visible-state", RememberForRepeat=decision.IsDialogue });
                        log.Write("structured-narration",decision.Text);
                    }
                    string openingText=openingReader.Update(decision.ImageIdentity,decision.ImageSlot,focused && narrator.Enabled && preferences.ReadOpening);
                    if(!String.IsNullOrWhiteSpace(openingText)) {narrator.Receive(new Dialogue {Text=openingText,Source="opening-ocr"});log.Write("opening-narration",openingText);}
                    if(openingReader.Error!=null) {status.Text=openingReader.Error;log.Write("opening-error",openingReader.Error);}
                }
                adapter.Poll();
            }
        } catch(Exception ex) { Fail(ex); }
    }
    protected override void WndProc(ref Message m) {
        if(m.Msg==GameShortcuts.Message && GameFocused()) { if(m.WParam.ToInt32()==1) { narrator.Repeat(); log.Write("repeat",true); } if(m.WParam.ToInt32()==2) Toggle(); }
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
        if((args.Length==5 || args.Length==6) && args[0]=="--setup-worker") return PortableSetup.Worker(root,args);
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
        if(args.Length>0 && args[0]=="--self-test-portable") return SelfTests.Run(root,null,true);
        if(args.Length>0 && args[0]=="--voice-test") return VoiceTests.Run(root);
        if(args.Length>0 && args[0]=="--speech-smoke") {
            trace("Creating speech engine");
            Directory.CreateDirectory(Path.Combine(root,"tests"));
            using(var output=new WindowsSpeech()) { trace("Engine created"); output.WriteWave(Path.Combine(root,"tests","speech-smoke.wav"),"Visual Novel Helper speech test."); trace("WAV generated"); } trace("Speech engine disposed"); return 0;
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
