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
    public static void Validate(string root,string game,bool checkCompatibility=true) {
        var profile=Json.Deserialize<GameProfile>(File.ReadAllText(Path.Combine(root,"profiles","fata.json")));
        var exe=Path.Combine(game,"fata.exe");
        if(checkCompatibility) GameCompatibility.Inspect(exe,profile);
        foreach(var process in Process.GetProcessesByName("fata")) using(process) {
            string path; try { path=process.MainModule.FileName; } catch { throw new InvalidOperationException("Close Fata Morgana before setup."); }
            if(Same(path,exe)) throw new InvalidOperationException("Close Fata Morgana before installing or removing its helper.");
        }
    }
    static InstallRecord Owned(string target,string record) {
        if(!File.Exists(record)) throw new InvalidOperationException("The existing integration has no ownership record.");
        InstallRecord info;
        try { info=Json.Deserialize<InstallRecord>(File.ReadAllText(record)); } catch { throw new InvalidOperationException("The helper ownership record is unreadable. Nothing was replaced."); }
        if(info==null || info.owner!=Owner || info.sha256!=Hash(target)) throw new InvalidOperationException("The installed extension has changed. Setup will not overwrite or remove it.");
        return info;
    }
    public static string BridgeBodyHash(string path) {
        string text=File.ReadAllText(path).Replace("\r\n","\n");
        int end=text.IndexOf('\n'); if(end<0) return null;
        string header=text.Substring(0,end);
        // Older development builds used a literal output path. Ignore only that declaration,
        // never arbitrary code or the rest of a bridge, when recognizing known versions.
        if(!Regex.IsMatch(header,"^var vnhOutputPath = \"(?:[^\"\\\\\r\n]|\\\\.)*\";$") &&
            header!="var vnhOutputPath = System.appDataPath + \"VisualNovelHelper/work/bridge-state.txt\";") return null;
        using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.Substring(end+1).TrimEnd('\r','\n')))).Replace("-","");
    }
    static InstallRecord Recognized(string root,string target,string record) {
        try { return Owned(target,record); } catch(InvalidOperationException) {}
        string bodyHash=BridgeBodyHash(target);
        string list=Path.Combine(root,"profiles","legacy-bridges.json");
        var known=File.Exists(list)?Json.Deserialize<string[]>(File.ReadAllText(list)):new string[0];
        if(bodyHash!=null && known!=null && Array.IndexOf(known,bodyHash)>=0)
            return new InstallRecord {owner=Owner,sha256=Hash(target)};
        throw new InvalidOperationException("An existing game extension is not a recognized VHVN version. It was left unchanged to protect other mods. Contact VHVN support with this message; you do not need to delete files yourself.");
    }
    public static string ExistingHash(string root,string game) {
        string target=Path.Combine(game,"AfterInit2.tjs");
        return File.Exists(target)?Recognized(root,target,Path.Combine(game,RecordName)).sha256:null;
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
    public static string Install(string root,string game,string data,string saves,bool remove,string approvedExistingHash=null) {
        game=Path.GetFullPath(game); data=Path.GetFullPath(data);
        Validate(root,game,!remove);
        string target=Path.Combine(game,"AfterInit2.tjs"), record=Path.Combine(game,RecordName);
        InstallRecord old=null;
        if(File.Exists(target)) old=remove?Owned(target,record):Recognized(root,target,record);
        if(remove) {
            if(old==null) return "The helper extension is already removed.";
            // Recheck ownership immediately before deleting the single owned extension.
            Owned(target,record); File.Delete(target); File.Delete(record);
            return "Removed the helper extension. Your saves, preferences and backups were kept.";
        }
        if(old!=null && old.sha256!=approvedExistingHash)
            throw new InvalidOperationException("The existing helper must be confirmed before replacement. Please choose Install / update again.");
        if(old==null && approvedExistingHash!=null)
            throw new InvalidOperationException("The game integration changed after confirmation. Please review setup again.");
        string stagedSource=Path.Combine(root,"bridge","AfterInit2.tjs");
        if(!File.Exists(stagedSource)) throw new FileNotFoundException("The helper installation is incomplete. Run the VHVN installer again.");
        Directory.CreateDirectory(Path.Combine(data,"work"));
        // Probe permission before copying saves, so an elevation retry creates just one backup.
        string temp=Path.Combine(game,".vnh-"+Guid.NewGuid().ToString("N")+".tmp");
        using(File.Open(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {}
        try {
            string backup=Path.Combine(data,"backups",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
            int count=CopyVerified(saves,Path.Combine(backup,"saves"));
            if(old!=null) {
                Directory.CreateDirectory(backup); string previous=Path.Combine(backup,"AfterInit2.tjs"); File.Copy(target,previous);
                if(Hash(previous)!=old.sha256) throw new IOException("The previous helper changed during backup. Nothing was replaced.");
                if(File.Exists(record)) File.Copy(record,Path.Combine(backup,RecordName));
            }
            File.Copy(stagedSource,temp,true); string hash=Hash(temp);
            // Record first: a crash can leave a conservative hash mismatch, never unowned deletion.
            string metadata=Json.Serialize(new InstallRecord {owner=Owner,sha256=hash});
            if(old!=null && Hash(target)!=approvedExistingHash) throw new IOException("The previous helper changed after confirmation. Nothing was replaced.");
            if(old==null && File.Exists(target)) throw new IOException("A game extension appeared during setup. Nothing was replaced.");
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
            WindowsSpeech voice=null; try { voice=new WindowsSpeech(); } catch(Exception) {}
            Action<string> speak=voice==null?null:(Action<string>)(text=> { voice.Stop(); voice.Speak(text); });
            action.Click+=delegate {
                try {
                    string game=Path.GetFullPath(folder.Text); Validate(root,game,!remove);
                    string approvedHash=null;
                    if(!remove) {
                        approvedHash=ExistingHash(root,game);
                        if(approvedHash!=null && !ConfirmReplacement(form,speak)) return;
                    }
                    string saves=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Mangagamer","The House in Fata Morgana");
                    string message;
                    try { message=Install(root,game,data,saves,remove,approvedHash); }
                    catch(UnauthorizedAccessException) {
                        if(new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) throw;
                        string arguments="--setup-worker "+Quote(game)+" "+Quote(data)+" "+Quote(saves)+" "+(remove?"remove":"install")+(approvedHash==null?"":" "+approvedHash);
                        using(var worker=Process.Start(new ProcessStartInfo(Application.ExecutablePath,arguments) {UseShellExecute=true,Verb="runas"})) { worker.WaitForExit(); if(worker.ExitCode!=0) return; }
                        completed=true; form.Close(); return;
                    }
                    completed=true; MessageBox.Show(form,message,"VHVN"); form.Close();
                } catch(Exception ex) { MessageBox.Show(form,ex.Message,"Setup could not finish",MessageBoxButtons.OK,MessageBoxIcon.Error); }
            };
            UiAccessibility.Wire(form,()=>voice!=null,speak);
            try { Application.Run(form); } finally { if(voice!=null) voice.Dispose(); }
            return completed?0:1;
        }
    }
    public static int Worker(string root,string[] args) {
        try { MessageBox.Show(Install(root,args[1],args[2],args[3],args[4]=="remove",args.Length==6?args[5]:null),"Visual Novel Helper"); return 0; }
        catch(Exception ex) { MessageBox.Show(ex.Message,"Setup could not finish",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1; }
    }
    public static void ConfirmationSmoke(string path) {
        if(ConfirmReplacement(null,null,path)) throw new Exception("Closing replacement confirmation must cancel.");
    }
    static bool ConfirmReplacement(Form owner,Action<string> speak,string smokePath=null) {
        using(var dialog=new Form {Text="Update existing VHVN helper?",ClientSize=new Size(600,230),Font=new Font("Segoe UI",12),AutoScaleDimensions=new SizeF(96,96),AutoScaleMode=AutoScaleMode.Dpi,AutoScroll=true,StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false}) {
            const string message="An existing VHVN helper was found in the game. Replace it with this version?\n\nYour saves and settings will be kept. The previous helper will be backed up. No folder cleanup is needed.";
            dialog.Controls.Add(new Label {Text=message,AccessibleName=message,Location=new Point(20,20),Size=new Size(560,130)});
            var yes=new Button {Text="&Replace helper",AccessibleName="Replace existing helper",DialogResult=DialogResult.Yes,Location=new Point(20,170),Size=new Size(230,40)};
            var no=new Button {Text="&Cancel",AccessibleName="Cancel and keep existing helper",DialogResult=DialogResult.Cancel,Location=new Point(350,170),Size=new Size(230,40)};
            dialog.Controls.Add(yes); dialog.Controls.Add(no); dialog.AcceptButton=yes; dialog.CancelButton=no;
            UiAccessibility.Wire(dialog,()=>speak!=null,speak);
            dialog.Shown+=delegate {
                if(speak!=null) speak(message+" Replace helper, or Cancel.");
                if(smokePath!=null) {
                    Application.DoEvents();
                    using(var bitmap=new Bitmap(dialog.Width,dialog.Height)) { dialog.DrawToBitmap(bitmap,new Rectangle(0,0,dialog.Width,dialog.Height)); bitmap.Save(smokePath); }
                    dialog.BeginInvoke((Action)(()=> { dialog.DialogResult=DialogResult.Cancel; dialog.Close(); }));
                }
            };
            return dialog.ShowDialog(owner)==DialogResult.Yes;
        }
    }
}
}
