using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace VisualNovelHelper {
public static class SelfTests {
    sealed class FakeSpeech : ISpeechOutput {
        public readonly List<string> Calls=new List<string>();
        public void Speak(string text) { Calls.Add("speak:"+text); }
        public void Stop() { Calls.Add("stop"); }
        public void Dispose() { }
    }
    static void Check(bool ok,string name) { if(!ok) throw new Exception("FAIL: "+name); }
    public static int Run(string root,string fixture) {
        var result=new List<string>();
        try {
            ViewSelfTests.Run(root);
            OpeningTests.Run();
            result.Add("PASS: opening OCR latest-scene selection, deduplication, focus-loss cancellation and disabled setting");
            result.Add("PASS: structured transport, TJS checksum, malformed/stale state rejection, source priority, hover dwell, view transitions and focus");
            var json=new JavaScriptSerializer();
            var profile=json.Deserialize<GameProfile>(File.ReadAllText(Path.Combine(root,"profiles","fata.json")));
            var prefs=json.Deserialize<Preferences>("{\"Voice\":\"\",\"Rate\":0,\"Volume\":85}");
            Check(prefs.RepeatKey==0 && prefs.ToggleKey==0,"old preferences migrate to unassigned keys");
            Check(prefs.RemapSkip && prefs.SkipKey==20 && prefs.ReadOpening,"old preferences receive accessible skip and opening defaults");
            int parsedKey,parsedModifiers; string parseError;
            Check(ShortcutDialog.TryParse("CapsLock",out parsedKey,out parsedModifiers,out parseError) && parsedKey==20,"Caps Lock can be typed");
            prefs.RepeatKey=119; prefs.RepeatModifiers=0;
            var restored=json.Deserialize<Preferences>(json.Serialize(prefs));
            Check(restored.RepeatKey==119 && restored.RepeatModifiers==0,"shortcut settings persisted");
            Check(ShortcutDialog.Describe(0,0)=="Unassigned" && ShortcutDialog.Describe(119,0)=="F8","shortcut labels");
            result.Add("PASS: shortcut migration, persistence and unmodified key labels");
            var parser=new FataLineParser(profile,0x828C,0x400000);
            string header="[18:828C:5240D0:52A999:12:KiriKiri1:HW-4*14:-4*0@1240D0:fata.exe] ";
            Check(parser.Parse(header+"One.Two").Text=="One. Two","sentence join");
            Check(parser.Parse(header.Replace("52A999","52A95A")+"Duplicate")==null,"duplicate stream");
            Check(parser.Parse(header.Replace("52A999","52AEDE")+"History")==null,"backlog stream");
            Check(parser.Parse(header.Replace(":12:",":FFFFFFF4:")+"UI")==null,"UI split");
            Check(parser.Parse(header.Replace("828C","0000")+"Clipboard")==null,"wrong process");
            Check(parser.Parse("unframed continuation")==null,"unframed text");
            var relocated=new FataLineParser(profile,0x828C,0x500000);
            Check(relocated.Parse(header.Replace("5240D0","6240D0").Replace("52A999","62A999")+"Relocated")!=null,"module relocation");
            result.Add("PASS: parser selection, noise rejection, sentence join, module relocation");
            var speech=new FakeSpeech(); var controller=new NarrationController(speech);
            controller.SetFocus(true); controller.Receive(new Dialogue {Text="First"}); controller.Receive(new Dialogue {Text="Next"});
            Check(String.Join("|",speech.Calls)=="stop|speak:First|stop|speak:Next","interrupt old speech");
            controller.Receive(new Dialogue {Text="Next"}); Check(speech.Calls.Count==6,"legitimate repeated text preserved");
            controller.SetFocus(false); int count=speech.Calls.Count;
            controller.Receive(new Dialogue {Text="Background"}); Check(speech.Calls.Count==count,"background silent");
            controller.SetFocus(true); Check(speech.Calls.Count==count,"no stale auto replay");
            controller.Toggle(); count=speech.Calls.Count; controller.Receive(new Dialogue {Text="Muted"}); controller.Repeat(); Check(speech.Calls.Count==count,"mute");
            controller.Toggle(); controller.Repeat(); Check(speech.Calls[speech.Calls.Count-1]=="speak:Muted","repeat latest");
            controller.Receive(new Dialogue {Text="Maid",RememberForRepeat=false});
            Check(speech.Calls[speech.Calls.Count-1]=="speak:Maid","name hover still speaks");
            controller.Repeat(); Check(speech.Calls[speech.Calls.Count-1]=="speak:Muted","speaker hover never replaces dialogue repeat");
            controller.Receive(new Dialogue {Text="Settings. Volume",RememberForRepeat=false});
            controller.Repeat(); Check(speech.Calls[speech.Calls.Count-1]=="speak:Muted","menu labels never replace dialogue repeat");
            controller.Receive(new Dialogue {Text="New passage"}); controller.Receive(new Dialogue {Text="Michel",RememberForRepeat=false});
            controller.Repeat(); Check(speech.Calls[speech.Calls.Count-1]=="speak:New passage","repeat follows latest dialogue after another name");
            controller.ClearDialogue(); count=speech.Calls.Count; controller.Repeat(); Check(speech.Calls.Count==count,"new connection cannot repeat stale dialogue");
            result.Add("PASS: cancellation, repeated passages, focus loss, mute and repeat");
            ShortcutTests.Run(); result.Add("PASS: shortcuts pass through outside game, exact modifiers, held-key suppression, focus transitions and native hook lifecycle");
            CompatibilityTests.StatusTests(); result.Add("PASS: modified-build connection waits for live bridge, reports missing text and recovers");
            if(fixture!=null) {
                int selected=0, other=0;
                foreach(var line in File.ReadLines(fixture)) { if(parser.Parse(line)!=null) selected++; else other++; }
                Check(selected>0,"real capture fixture"); result.Add("PASS: pasted capture replay selected "+selected+" dialogue emissions; ignored "+other+" other lines");
            }
            File.WriteAllLines(Path.Combine(root,"tests","self-test-results.txt"),result); return 0;
        } catch(Exception ex) { result.Add(ex.ToString()); File.WriteAllLines(Path.Combine(root,"tests","self-test-results.txt"),result); return 1; }
    }
}
}
