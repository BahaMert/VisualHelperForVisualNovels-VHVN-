using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VisualNovelHelper {
public static class PortablePaths {
    public static string Data(string root) {
        return File.Exists(Path.Combine(root,"portable.txt")) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"VisualNovelHelper") : root;
    }
}
public sealed class InstallRecord { public string owner, sha256; }
public static class PortableSetup {
    const string Owner="VisualNovelHelper-Fata-v1";
    const string RecordName="VisualNovelHelper.install.json";
    static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
    public static bool NeedsSetup(string root,string data) {
        try {
            string saved=Path.Combine(data,"game-folder.txt");
            if(!File.Exists(saved)) return true;
            string game=File.ReadAllText(saved).Trim();
            if(!Path.IsPathRooted(game) || !File.Exists(Path.Combine(game,"fata.exe"))) return true;
            string target=Path.Combine(game,"AfterInit2.tjs");
            var record=Owned(target,Path.Combine(game,RecordName));
            return record.sha256!=Hash(Path.Combine(root,"bridge","AfterInit2.tjs"));
        } catch { return true; }
    }
    public static int RemoveInstalled(string root) {
        try {
            string data=PortablePaths.Data(root), saved=Path.Combine(data,"game-folder.txt");
            if(!File.Exists(saved)) return 0;
            string game=File.ReadAllText(saved).Trim();
            if(!Path.IsPathRooted(game)) throw new InvalidOperationException("The saved game folder is invalid. Open game setup to correct it before uninstalling.");
            if(!File.Exists(Path.Combine(game,"AfterInit2.tjs"))) return 0;
            string saves=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Mangagamer","The House in Fata Morgana");
            try { Install(root,game,data,saves,true); }
            catch(UnauthorizedAccessException) {
                using(var worker=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--setup-worker "+Quote(game)+" "+Quote(data)+" "+Quote(saves)+" remove") {UseShellExecute=true,Verb="runas"})) {
                    worker.WaitForExit(); return worker.ExitCode;
                }
            }
            return 0;
        } catch(Exception ex) { MessageBox.Show(ex.Message,"VHVN could not remove game integration",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1; }
    }
    public static string Hash(string path) {
        using(var sha=SHA256.Create()) using(var stream=File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
    }
    static bool Same(string a,string b) { return String.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase); }
    public static void Validate(string root,string game) {
        var profile=Json.Deserialize<GameProfile>(File.ReadAllText(Path.Combine(root,"profiles","fata.json")));
        var exe=Path.Combine(game,"fata.exe");
        if(!File.Exists(exe)) throw new InvalidOperationException("Choose the game folder containing fata.exe.");
        if(!String.Equals(Hash(exe),profile.exeSha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("This game version has not been tested. This package supports the English Steam build listed in START HERE.txt.");
        foreach(var process in Process.GetProcessesByName("fata")) using(process) {
            string path; try { path=process.MainModule.FileName; } catch { throw new InvalidOperationException("Close Fata Morgana before setup."); }
            if(Same(path,exe)) throw new InvalidOperationException("Close Fata Morgana before installing or removing its helper.");
        }
    }
    static InstallRecord Owned(string target,string record) {
        if(!File.Exists(record)) throw new InvalidOperationException("An existing AfterInit2.tjs belongs to another installation. Nothing was replaced. Remove that extension using its original installer first.");
        InstallRecord info;
        try { info=Json.Deserialize<InstallRecord>(File.ReadAllText(record)); } catch { throw new InvalidOperationException("The helper ownership record is unreadable. Nothing was replaced."); }
        if(info==null || info.owner!=Owner || info.sha256!=Hash(target)) throw new InvalidOperationException("The installed extension has changed. Setup will not overwrite or remove it.");
        return info;
    }
    static int CopyVerified(string source,string destination) {
        if(!Directory.Exists(source)) return 0;
        if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0) throw new IOException("Save backup will not follow redirected folders.");
        Directory.CreateDirectory(destination); int count=0;
        foreach(string file in Directory.GetFiles(source)) {
            if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0) throw new IOException("Save backup will not follow file links.");
            var copy=Path.Combine(destination,Path.GetFileName(file)); File.Copy(file,copy,false);
            if(Hash(file)!=Hash(copy)) throw new IOException("Save backup verification failed; installation stopped."); count++;
        }
        foreach(string child in Directory.GetDirectories(source)) count+=CopyVerified(child,Path.Combine(destination,Path.GetFileName(child)));
        return count;
    }
    public static string Install(string root,string game,string data,string saves,bool remove) {
        game=Path.GetFullPath(game); data=Path.GetFullPath(data);
        Validate(root,game);
        string target=Path.Combine(game,"AfterInit2.tjs"), record=Path.Combine(game,RecordName);
        InstallRecord old=null;
        if(File.Exists(target)) old=Owned(target,record);
        if(remove) {
            if(old==null) return "The helper extension is already removed.";
            // Recheck ownership immediately before deleting the single owned extension.
            Owned(target,record); File.Delete(target); File.Delete(record);
            return "Removed the helper extension. Your saves, preferences and backups were kept.";
        }
        string stagedSource=Path.Combine(root,"bridge","AfterInit2.tjs");
        if(!File.Exists(stagedSource)) throw new FileNotFoundException("The helper installation is incomplete. Run the VHVN installer again.");
        Directory.CreateDirectory(Path.Combine(data,"work"));
        // Probe permission before copying saves, so an elevation retry creates just one backup.
        string temp=Path.Combine(game,".vnh-"+Guid.NewGuid().ToString("N")+".tmp");
        using(File.Open(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {}
        try {
            string backup=Path.Combine(data,"backups",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
            int count=CopyVerified(saves,Path.Combine(backup,"saves"));
            if(old!=null) { Directory.CreateDirectory(backup); File.Copy(target,Path.Combine(backup,"AfterInit2.tjs")); }
            File.Copy(stagedSource,temp,true); string hash=Hash(temp);
            // Record first: a crash can leave a conservative hash mismatch, never unowned deletion.
            string metadata=Json.Serialize(new InstallRecord {owner=Owner,sha256=hash});
            if(old!=null) Owned(target,record);
            File.WriteAllText(record,metadata,Encoding.UTF8);
            if(old==null) File.Move(temp,target); else File.Replace(temp,target,null);
            if(Hash(target)!=hash) throw new IOException("Installed extension hash verification failed.");
            File.WriteAllText(Path.Combine(data,"game-folder.txt"),game,Encoding.UTF8);
            return "Ready. "+count+" save files backed up and verified.\n\nStart the game through Steam. VHVN will connect when the game opens.\nHover a speaker's printed name to hear it.";
        } finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    public static IEnumerable<string> LibraryPaths(string text) {
        foreach(Match m in Regex.Matches(text,"\"(?:path|[0-9]+)\"\\s*\"([^\"]+)\"")) {
            string path=m.Groups[1].Value.Replace("\\\\","\\");
            if(Path.IsPathRooted(path)) yield return path;
        }
    }
    public static List<string> FindGames() {
        var libraries=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var key in new[]{@"HKEY_CURRENT_USER\Software\Valve\Steam",@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"}) {
            foreach(var name in new[]{"SteamPath","InstallPath"}) {
                var value=Registry.GetValue(key,name,null) as string; if(!String.IsNullOrEmpty(value)) libraries.Add(value);
            }
        }
        libraries.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
        foreach(var steam in libraries.ToArray()) {
            string file=Path.Combine(steam,"steamapps","libraryfolders.vdf");
            try { if(File.Exists(file)) foreach(string path in LibraryPaths(File.ReadAllText(file))) libraries.Add(path); } catch(IOException) {} catch(UnauthorizedAccessException) {}
        }
        var result=new List<string>();
        foreach(string library in libraries) {
            string game=Path.Combine(library,"steamapps","common","The House in Fata Morgana");
            if(File.Exists(Path.Combine(game,"fata.exe"))) result.Add(game);
        }
        return result;
    }
    static string Quote(string value) {
        var b=new StringBuilder("\""); int slashes=0;
        foreach(char c in value) {
            if(c=='\\') { slashes++; continue; }
            b.Append('\\',c=='"'?slashes*2+1:slashes); b.Append(c); slashes=0;
        }
        b.Append('\\',slashes*2).Append('"'); return b.ToString();
    }
    public static int Run(string root,bool remove) {
        bool completed=false;
        string data=PortablePaths.Data(root); Directory.CreateDirectory(Path.Combine(data,"work"));
        using(var form=new Form {Text=remove?"Remove VHVN game integration":"VHVN — first-time game setup",ClientSize=new Size(640,240),Font=new Font("Segoe UI",12),AutoScroll=true,AutoScaleMode=AutoScaleMode.Dpi,AutoScaleDimensions=new SizeF(96,96),StartPosition=FormStartPosition.CenterScreen}) {
            form.Controls.Add(new Label {Text="The House in Fata Morgana — English Steam build\nClose the game, then choose its folder.",Location=new Point(20,15),Size=new Size(600,55)});
            var folder=new TextBox {Location=new Point(20,85),Width=485,AccessibleName="Game folder"}; form.Controls.Add(folder);
            var found=FindGames(); if(found.Count>0) folder.Text=found[0];
            var saved=Path.Combine(data,"game-folder.txt"); if(File.Exists(saved) && Directory.Exists(File.ReadAllText(saved))) folder.Text=File.ReadAllText(saved);
            var browse=new Button {Text="Browse…",Location=new Point(515,82),Size=new Size(105,34)}; form.Controls.Add(browse);
            browse.Click+=delegate { using(var chooser=new FolderBrowserDialog {Description="Select the folder containing fata.exe",SelectedPath=folder.Text}) if(chooser.ShowDialog(form)==DialogResult.OK) folder.Text=chooser.SelectedPath; };
            var action=new Button {Text=remove?"Remove helper":"Install / update",Location=new Point(20,145),Size=new Size(190,40)}; form.Controls.Add(action); form.AcceptButton=action;
            form.Controls.Add(new Label {Text="Windows may request permission to write in the game folder.\nThe reader itself runs normally; saves are preserved.",Location=new Point(225,143),Size=new Size(395,65)});
            action.Click+=delegate {
                try {
                    string game=Path.GetFullPath(folder.Text); Validate(root,game);
                    string saves=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Mangagamer","The House in Fata Morgana");
                    string message;
                    try { message=Install(root,game,data,saves,remove); }
                    catch(UnauthorizedAccessException) {
                        if(new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) throw;
                        string arguments="--setup-worker "+Quote(game)+" "+Quote(data)+" "+Quote(saves)+" "+(remove?"remove":"install");
                        using(var worker=Process.Start(new ProcessStartInfo(Application.ExecutablePath,arguments) {UseShellExecute=true,Verb="runas"})) { worker.WaitForExit(); if(worker.ExitCode!=0) return; }
                        completed=true; form.Close(); return;
                    }
                    completed=true; MessageBox.Show(form,message,"VHVN"); form.Close();
                } catch(Exception ex) { MessageBox.Show(form,ex.Message,"Setup could not finish",MessageBoxButtons.OK,MessageBoxIcon.Error); }
            };
            WindowsSpeech voice=null; try { voice=new WindowsSpeech(); } catch(Exception) {}
            UiAccessibility.Wire(form,()=>voice!=null,voice==null?null:(Action<string>)(text=> { voice.Stop(); voice.Speak(text); }));
            try { Application.Run(form); } finally { if(voice!=null) voice.Dispose(); }
            return completed?0:1;
        }
    }
    public static int Worker(string root,string[] args) {
        try { MessageBox.Show(Install(root,args[1],args[2],args[3],args[4]=="remove"),"Visual Novel Helper"); return 0; }
        catch(Exception ex) { MessageBox.Show(ex.Message,"Setup could not finish",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1; }
    }
}
}
