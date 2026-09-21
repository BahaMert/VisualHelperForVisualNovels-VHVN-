using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace VisualNovelHelper {
public sealed class GameProfile {
    public string name, processName, exeSha256, hookName, hookOffset, contextOffset, context2, cliRelativePath;
}
public sealed class Preferences {
    public string Voice = "";
    public int Rate = 0;
    public int Volume = 85;
    public bool ReadControls = true;
    public bool RemapSkip = true, ReadOpening = true;
    public int SkipKey = 20, SkipModifiers = 0; // Hold Caps Lock; no toggle state is changed.
    public int RepeatKey = 0, RepeatModifiers = 0, ToggleKey = 0, ToggleModifiers = 0;
}
public sealed class Log : IDisposable {
    readonly StreamWriter writer;
    readonly JavaScriptSerializer json = new JavaScriptSerializer();
    bool disposed;
    public Log(string file) { writer = new StreamWriter(file, false, new UTF8Encoding(false)); writer.AutoFlush = true; }
    public void Write(string kind, object value) { lock(writer) { if(!disposed) writer.WriteLine(json.Serialize(new { utc = DateTime.UtcNow.ToString("o"), kind = kind, value = value })); } }
    public void Dispose() { lock(writer) { disposed=true; writer.Dispose(); } }
}
public sealed class Dialogue {
    public string Text;
    public string Source;
    public bool RememberForRepeat=true;
}
public interface ISpeechOutput : IDisposable {
    void Speak(string text);
    void Stop();
}
public sealed class NarrationController {
    readonly ISpeechOutput speech;
    public string Current { get; private set; }
    public bool Enabled { get; private set; }
    public bool Focused { get; private set; }
    public NarrationController(ISpeechOutput output) { speech=output; Enabled=true; }
    public void SetFocus(bool focused) { if (Focused && !focused) speech.Stop(); Focused=focused; }
    public void Receive(Dialogue line) {
        if(line.RememberForRepeat) Current=line.Text;
        // Both semantic passages and fallback capture emissions replace current speech.
        if (Enabled && Focused) { speech.Stop(); speech.Speak(line.Text); }
    }
    public void Toggle() { Enabled=!Enabled; if (!Enabled) speech.Stop(); }
    public void Repeat() { if (Enabled && !String.IsNullOrWhiteSpace(Current)) { speech.Stop(); speech.Speak(Current); } }
    public void Stop() { speech.Stop(); }
    public void ClearDialogue() { Current=null; }
}
public sealed class FataLineParser {
    static readonly Regex Header = new Regex(@"^\[([0-9A-F]+):([0-9A-F]+):([0-9A-F]+):([0-9A-F]+):([0-9A-F]+):([^:]+):([^\]]+)\] ?(.*)$", RegexOptions.IgnoreCase);
    readonly GameProfile profile;
    readonly int pid;
    readonly ulong moduleBase;
    public FataLineParser(GameProfile p, int processId, ulong imageBase) { profile=p; pid=processId; moduleBase=imageBase; }
    public Dialogue Parse(string line) {
        var m=Header.Match(line);
        if (!m.Success || Convert.ToUInt64(m.Groups[2].Value,16)!=(ulong)pid || m.Groups[6].Value!=profile.hookName) return null;
        if (Convert.ToUInt64(m.Groups[3].Value,16)!=moduleBase+Convert.ToUInt64(profile.hookOffset,16) ||
            Convert.ToUInt64(m.Groups[4].Value,16)!=moduleBase+Convert.ToUInt64(profile.contextOffset,16) ||
            Convert.ToUInt64(m.Groups[5].Value,16)!=Convert.ToUInt64(profile.context2,16)) return null;
        var text=m.Groups[8].Value.Trim();
        if (text.Length==0 || !Regex.IsMatch(text,@"[\p{L}\p{N}]")) return null;
        // Conservative repair for observed sentence joins. Preserve punctuation and repeated sentences.
        text=Regex.Replace(text,@"([.!?])(?=[A-Z])", "$1 ");
        return new Dialogue { Text=text, Source=m.Groups[1].Value+":"+m.Groups[4].Value+":"+m.Groups[5].Value };
    }
    public object InspectGameLine(string line) {
        var m=Header.Match(line);
        if(!m.Success || Convert.ToUInt64(m.Groups[2].Value,16)!=(ulong)pid) return null;
        var hook=m.Groups[6].Value;
        // Restrict diagnostic retention to known engine text and menu text calls.
        if(hook!="KiriKiri1" && hook!="DrawTextA" && hook!="DrawTextW" && hook!="DrawTextExA" && hook!="DrawTextExW") return null;
        var address=Convert.ToUInt64(m.Groups[3].Value,16);
        var context=Convert.ToUInt64(m.Groups[4].Value,16);
        return new { hook=hook, address=address.ToString("X"), context=context.ToString("X"), relativeContext=context>=moduleBase?(context-moduleBase).ToString("X"):null, split=m.Groups[5].Value, text=m.Groups[8].Value };
    }
}
}
