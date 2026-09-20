using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;

namespace VisualNovelHelper {
public interface IGameAdapter : IDisposable {
    int GamePid { get; }
    void Start();
    void Poll();
    event Action<Dialogue> DialogueReceived;
    event Action<string> Status;
}
public sealed class TextractorAdapter : IGameAdapter {
    readonly string root;
    readonly GameProfile profile;
    readonly Log log;
    readonly ConcurrentQueue<string> lines=new ConcurrentQueue<string>();
    Process child, game;
    FataLineParser parser;
    DateTime launched, lastOutput;
    bool attached, stopping, ready;
    public int GamePid { get; private set; }
    public event Action<Dialogue> DialogueReceived;
    public event Action<string> Status;
    public TextractorAdapter(string projectRoot, GameProfile p, Log logger) { root=projectRoot; profile=p; log=logger; }
    public void Start() {
        if (Process.GetProcessesByName("TextractorCLI").Length>0 || Process.GetProcessesByName("Textractor").Length>0)
            throw new InvalidOperationException("Close the capture experiment and Textractor before starting this helper.");
        var games=Process.GetProcessesByName(profile.processName);
        if (games.Length!=1) throw new InvalidOperationException("Open one copy of Fata Morgana first, then press Connect.");
        game=games[0]; GamePid=game.Id;
        var module=game.MainModule;
        string hash;
        using (var sha=SHA256.Create()) using(var f=File.OpenRead(module.FileName)) hash=BitConverter.ToString(sha.ComputeHash(f)).Replace("-","");
        if (!String.Equals(hash,profile.exeSha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("This game build differs from the tested build. Update its adapter profile before attaching.");
        parser=new FataLineParser(profile,GamePid,(ulong)module.BaseAddress.ToInt64());
        var path=Path.Combine(root,profile.cliRelativePath.Replace('/',Path.DirectorySeparatorChar));
        child=new Process();
        child.StartInfo=new ProcessStartInfo(path) { WorkingDirectory=Path.GetDirectoryName(path), UseShellExecute=false, CreateNoWindow=true,
            RedirectStandardInput=true, RedirectStandardOutput=true, RedirectStandardError=true, StandardOutputEncoding=Encoding.Unicode };
        child.OutputDataReceived+=(s,e)=> { if(e.Data!=null) lines.Enqueue(e.Data); };
        child.ErrorDataReceived+=(s,e)=> { if(!String.IsNullOrWhiteSpace(e.Data)) log.Write("cli-stderr",e.Data); };
        if(!child.Start()) throw new InvalidOperationException("Could not start capture tool.");
        child.BeginOutputReadLine(); child.BeginErrorReadLine();
        launched=lastOutput=DateTime.UtcNow;
        log.Write("adapter-start",new { gamePid=GamePid, build=hash });
        Notify("Preparing capture...");
    }
    void Notify(string value) { log.Write("status",value); if(Status!=null) Status(value); }
    void Command(string text) { var bytes=Encoding.Unicode.GetBytes(text+"\n"); child.StandardInput.BaseStream.Write(bytes,0,bytes.Length); child.StandardInput.BaseStream.Flush(); }
    public void Poll() {
        if(stopping || child==null) return;
        if(game.HasExited) throw new InvalidOperationException("The game has closed. Close the helper or reopen the game and Connect.");
        if(child.HasExited) throw new InvalidOperationException("Capture stopped unexpectedly. Close this helper, restart the game, and retry.");
        string line;
        // Stock CLI emits the initial clipboard before attachment. Discard startup output entirely;
        // this is a prototype workaround, not a production framing protocol.
        while(lines.TryDequeue(out line)) {
            lastOutput=DateTime.UtcNow;
            if(!attached) continue;
            if(line.StartsWith("[0:0:") && line.Contains(":Console:")) {
                if(line.Contains("couldn't inject")) throw new InvalidOperationException("Capture could not attach. Run the helper normally from Explorer under the same account as the game.");
                if(line.Contains("already injected")) throw new InvalidOperationException("A previous hook is still attached. Close the helper, restart the game, and retry.");
                if(line.Contains("inserting hook: KiriKiri1")) { ready=true; Notify("Connected. Return to the game and advance to the next passage."); }
                continue;
            }
            var dialogue=parser.Parse(line);
            var diagnostic=parser.InspectGameLine(line);
            if(diagnostic!=null) log.Write("game-text-diagnostic",diagnostic);
            if(dialogue!=null) {
                log.Write("dialogue",new { text=dialogue.Text, source=dialogue.Source });
                if(DialogueReceived!=null) DialogueReceived(dialogue);
            }
        }
        if(!attached && (DateTime.UtcNow-launched).TotalSeconds>1.5 && (DateTime.UtcNow-lastOutput).TotalMilliseconds>500) {
            Command("attach -P"+GamePid); attached=true; launched=DateTime.UtcNow; Notify("Connecting to the game...");
        }
        if(attached && !ready && (DateTime.UtcNow-launched).TotalSeconds>15) throw new InvalidOperationException("The dialogue hook was not confirmed within 15 seconds. Restart the game before retrying.");
    }
    public void Dispose() {
        if(stopping) return; stopping=true;
        if(child!=null) {
            try {
                if(!child.HasExited && attached && ready) {
                    Command("detach -P"+GamePid);
                    // Host::DetachProcess writes asynchronously. Keep its process alive for delivery.
                    Thread.Sleep(1200);
                }
                if(!child.HasExited) { child.StandardInput.Close(); if(!child.WaitForExit(2000)) child.Kill(); }
                child.WaitForExit();
            } catch(Exception ex) { log.Write("cleanup-error",ex.Message); }
            child.Dispose(); child=null;
        }
        if(game!=null) game.Dispose();
    }
}
}
