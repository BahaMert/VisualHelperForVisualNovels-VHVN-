using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VisualNovelHelper {
public static class VoiceTests {
    static void Check(bool ok,string message) { if(!ok) throw new Exception("FAIL: "+message); }
    public static void SelectionTests() {
        var desktop=new SpeechVoice("desktop-id","Microsoft David Desktop","Microsoft David Desktop - English (United States)");
        var natural=new SpeechVoice("natural-id","Jenny","Jenny (Natural)");
        var duplicate=new SpeechVoice("other-provider-id","Jenny","Jenny (Natural)");
        var list=new List<SpeechVoice> {desktop,natural,duplicate};
        Check(WindowsSpeech.Resolve(list,"NATURAL-ID",null)==natural,"natural voice restored by token ID");
        Check(WindowsSpeech.Resolve(list,"other-provider-id","Jenny")==duplicate,"same-name voices keep distinct identity");
        Check(WindowsSpeech.Resolve(list,"","Microsoft David Desktop")==desktop,"old System.Speech name migrates");
        Check(WindowsSpeech.Resolve(list,null,desktop.Description)==desktop,"description-only preference migrates");
        Check(WindowsSpeech.Resolve(list,"removed-id","Jenny")==null,"missing token never switches providers by name");
        Check(WindowsSpeech.Resolve(new List<SpeechVoice>(),"","")==null,"empty voice list handled");
    }
    public static int Run(string root) {
        string folder=Path.Combine(root,"tests"); Directory.CreateDirectory(folder);
        var results=new List<string>();
        try {
            SelectionTests(); results.Add("PASS: voice identity, name migration, missing voices and duplicate names");
            using(var speech=new WindowsSpeech()) {
                speech.Failed+=message=> { throw new Exception(message); };
                Check(speech.Voices.Count>0,"native voice enumeration");
                var initial=speech.DefaultVoice;
                speech.Configure(initial,0,85);
                speech.Refresh();
                var restored=WindowsSpeech.Resolve(speech.Voices,initial.Id,null);
                Check(restored!=null,"refresh retains token identity");
                speech.Configure(restored,0,85);
                bool rejected=false;
                try { speech.Configure(new SpeechVoice("nonexistent","missing","missing"),0,85); }
                catch(InvalidOperationException) { rejected=true; }
                Check(rejected && speech.SelectedId==initial.Id,"invalid selection preserves current voice");
                results.Add("PASS: native SAPI discovery, refresh and invalid-selection recovery");
                int i=0;
                foreach(var voice in speech.Voices) {
                    speech.Configure(voice,0,85);
                    string path=Path.Combine(folder,"voice-"+(i++)+".wav");
                    // Leading angle bracket exercises SVSFIsNotXML, not SAPI markup.
                    speech.WriteWave(path,"<This is dialogue, not XML.> Visual Novel Helper voice test.");
                    byte[] bytes=File.ReadAllBytes(path);
                    Check(bytes.Length>1000 && Encoding.ASCII.GetString(bytes,0,4)=="RIFF" && Encoding.ASCII.GetString(bytes,8,4)=="WAVE","voice produced WAV audio: "+voice.Description);
                    results.Add("PASS: WAV synthesis and literal dialogue: "+voice.Description);
                    var elapsed=System.Diagnostics.Stopwatch.StartNew();
                    speech.WriteWave(Path.Combine(folder,"cancel-"+i+".wav"),"Next passage.",true);
                    Check(elapsed.Elapsed.TotalSeconds<10,"cancelled passage cannot delay replacement speech");
                    results.Add("PASS: native cancellation and replacement: "+voice.Description);
                }
                speech.Stop(); speech.Stop(); speech.Dispose(); speech.Dispose();
                results.Add("PASS: repeated stop and disposal");
            }
            File.WriteAllLines(Path.Combine(folder,"voice-test-results.txt"),results); return 0;
        } catch(Exception ex) {
            results.Add(ex.ToString()); File.WriteAllLines(Path.Combine(folder,"voice-test-results.txt"),results); return 1;
        }
    }
}
}
