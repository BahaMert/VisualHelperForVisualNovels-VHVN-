using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace VisualNovelHelper {
public sealed class SpeechVoice {
    public readonly string Id, Name, Description;
    public SpeechVoice(string id,string name,string description) { Id=id; Name=name; Description=description; }
    public override string ToString() { return Description; }
}

// Keep native SAPI descriptions and token identities together for discovery,
// selection and playback, including dynamic tokens supplied by voice adapters.
// All calls are made on the owning STA (the WinForms UI thread).
public sealed class WindowsSpeech : ISpeechOutput {
    const int Async=1, Purge=2, PlainText=16;
    dynamic engine;
    readonly Dictionary<string,object> tokens=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
    readonly List<SpeechVoice> voices=new List<SpeechVoice>();
    bool pending;
    public event Action<string> Failed;
    public event Action<string> Diagnostic;
    public IList<SpeechVoice> Voices { get { return voices.AsReadOnly(); } }
    public string SelectedId { get; private set; }

    public WindowsSpeech() {
        try {
            engine=Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpVoice",true));
            Refresh();
        } catch { Dispose(); throw; }
    }
    internal static void Release(object value) {
        if(value!=null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }
    public void Refresh() {
        Stop();
        var nextTokens=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
        var nextVoices=new List<SpeechVoice>();
        object collection=null;
        try {
            collection=engine.GetVoices("","");
            dynamic available=collection;
            int count=available.Count;
            for(int i=0;i<count;i++) {
                object value=available.Item(i);
                try {
                    dynamic token=value;
                    string id=token.Id, description=token.GetDescription(0), name=description;
                    try { name=token.GetAttribute("Name"); } catch(COMException) { }
                    if(nextTokens.ContainsKey(id)) continue;
                    nextTokens.Add(id,value); value=null;
                    nextVoices.Add(new SpeechVoice(id,name,description));
                } finally { Release(value); }
            }
            if(nextVoices.Count==0) throw new InvalidOperationException("No Windows SAPI voices are available. Install a Windows speech voice or a compatible 64-bit voice adapter.");
            foreach(object value in tokens.Values) Release(value);
            tokens.Clear(); voices.Clear();
            foreach(var pair in nextTokens) tokens.Add(pair.Key,pair.Value);
            nextTokens.Clear(); voices.AddRange(nextVoices);
        } finally {
            Release(collection);
            foreach(object value in nextTokens.Values) Release(value);
        }
    }
    public static SpeechVoice Resolve(IList<SpeechVoice> available,string id,string oldName) {
        foreach(var voice in available) if(String.Equals(voice.Id,id,StringComparison.OrdinalIgnoreCase)) return voice;
        // Migrate old name-only preferences, but never silently substitute another
        // provider for a missing saved token with an identical display name.
        if(String.IsNullOrEmpty(id)) foreach(var voice in available)
            if(String.Equals(voice.Name,oldName,StringComparison.OrdinalIgnoreCase) || String.Equals(voice.Description,oldName,StringComparison.OrdinalIgnoreCase)) return voice;
        return null;
    }
    public SpeechVoice DefaultVoice {
        get {
            object token=null;
            try {
                token=engine.Voice;
                var match=Resolve(voices,(string)((dynamic)token).Id,null);
                if(match!=null) return match;
            } catch(COMException) { }
            finally { Release(token); }
            return voices[0];
        }
    }
    public void Configure(SpeechVoice voice,int rate,int volume) {
        object token;
        if(voice==null || !tokens.TryGetValue(voice.Id,out token)) throw new InvalidOperationException("The selected voice is no longer available. Refresh voices and choose another voice.");
        Stop();
        engine.Voice=token;
        engine.Rate=Math.Max(-10,Math.Min(10,rate));
        engine.Volume=Math.Max(0,Math.Min(100,volume));
        SelectedId=voice.Id;
    }
    public void Speak(string text) {
        if(engine==null || String.IsNullOrWhiteSpace(text)) return;
        try {
            // Game text must never be interpreted as speech XML or file names.
            engine.Speak(text,Async|PlainText); pending=true;
            if(Diagnostic!=null) Diagnostic("speech-queued");
        } catch(Exception ex) { ReportFailure(ex); }
    }
    public void Stop() {
        if(engine==null || !pending) return;
        bool wasPending=pending; pending=false;
        try { engine.Speak("",Async|Purge|PlainText); }
        catch(COMException) { /* Cleanup must work even after an engine failure. */ }
        catch(UnauthorizedAccessException) { }
        if(wasPending && Diagnostic!=null) Diagnostic("speech-stopped");
    }
    public void Poll() {
        if(!pending || engine==null) return;
        try {
            if(!engine.WaitUntilDone(0)) return;
            pending=false;
            object state=engine.Status;
            try {
                int result=((dynamic)state).LastHResult;
                if(result<0) Marshal.ThrowExceptionForHR(result);
            } finally { Release(state); }
            if(Diagnostic!=null) Diagnostic("speech-completed");
        } catch(Exception ex) { ReportFailure(ex); }
    }
    void ReportFailure(Exception ex) {
        pending=false;
        if(Failed!=null) Failed("The selected voice could not speak. Choose another voice or check Natural voices / setup. "+ex.Message);
    }
    // Bounded, silent native integration check; never sends test audio to speakers.
    internal void WriteWave(string path,string text,bool testCancellation=false) {
        Stop();
        dynamic stream=Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpFileStream",true));
        try {
            stream.Open(Path.GetFullPath(path),3,false); // SSFMCreateForWrite
            engine.AudioOutputStream=stream;
            if(testCancellation) {
                // A queued long utterance must not delay the next passage.
                Speak(new string('a',10000));
                Stop();
                if(!engine.WaitUntilDone(3000)) throw new TimeoutException("Voice did not cancel within three seconds.");
            }
            engine.Speak(text,Async|PlainText);
            pending=true;
            if(!engine.WaitUntilDone(30000)) throw new TimeoutException("Voice did not finish the speech test within 30 seconds.");
        } finally {
            Stop();
            try { engine.AudioOutputStream=null; }
            finally { try { stream.Close(); } finally { Release((object)stream); } }
        }
    }
    public void Dispose() {
        try { Stop(); }
        finally {
            Release((object)engine); engine=null;
            foreach(object token in tokens.Values) Release(token);
            tokens.Clear(); voices.Clear();
        }
    }
}
}
