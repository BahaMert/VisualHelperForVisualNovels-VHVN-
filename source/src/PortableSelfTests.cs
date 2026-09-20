using System;
using System.IO;
using System.Linq;
namespace VisualNovelHelper {
public static class PortableSelfTests {
    static void Check(bool value,string name) { if(!value) throw new Exception(name); }
    static void Refuses(Action action,string name) {
        bool refused=false; try { action(); } catch(InvalidOperationException) { refused=true; }
        Check(refused,name);
    }
    public static int Run(string root,string fixtureExe,string workspace) {
        Directory.CreateDirectory(workspace);
        try {
            string game=Path.Combine(workspace,"Game with spaces"), data=Path.Combine(workspace,"New user's state"), saves=Path.Combine(workspace,"Invented saves");
            Directory.CreateDirectory(game); Directory.CreateDirectory(saves);
            File.Copy(fixtureExe,Path.Combine(game,"fata.exe"),true);
            File.WriteAllText(Path.Combine(saves,"test.save"),"invented save fixture");
            string target=Path.Combine(game,"AfterInit2.tjs");
            Check(PortableSetup.NeedsSetup(root,data),"first launch requests setup");
            File.WriteAllText(target,"unrelated extension");
            Refuses(()=>PortableSetup.Install(root,game,data,saves,false),"unknown extension refused");
            Check(File.ReadAllText(target)=="unrelated extension","unrelated extension unchanged");
            File.Delete(target); // Only this test's known single file.
            PortableSetup.Install(root,game,data,saves,false);
            Check(!PortableSetup.NeedsSetup(root,data),"configured launch skips setup");
            string hash=PortableSetup.Hash(target);
            Check(hash==PortableSetup.Hash(Path.Combine(root,"bridge","AfterInit2.tjs")),"installed package bytes");
            Check(File.ReadAllText(Path.Combine(saves,"test.save"))=="invented save fixture","original save unchanged");
            Check(Directory.GetFiles(Path.Combine(data,"backups"),"test.save",SearchOption.AllDirectories).Length==1,"verified save backup");
            int backupsBefore=Directory.GetDirectories(Path.Combine(data,"backups")).Length;
            Refuses(()=>PortableSetup.Install(root,game,data,saves,false),"existing helper requires explicit confirmation");
            Check(PortableSetup.Hash(target)==hash && Directory.GetDirectories(Path.Combine(data,"backups")).Length==backupsBefore,"cancelled replacement changes nothing");
            PortableSetup.Install(root,game,data,saves,false,hash);
            Check(PortableSetup.Hash(target)==hash,"owned update");
            File.AppendAllText(target," changed");
            Check(PortableSetup.NeedsSetup(root,data),"modified bridge requires attention");
            Refuses(()=>PortableSetup.Install(root,game,data,saves,true),"modified extension removal refused");
            Refuses(()=>PortableSetup.Install(root,game,data,saves,false),"modified extension update refused");
            File.Copy(Path.Combine(root,"bridge","AfterInit2.tjs"),target,true);
            PortableSetup.Install(root,game,data,saves,true);
            Check(!File.Exists(target) && !File.Exists(Path.Combine(game,"VisualNovelHelper.install.json")),"only owned files uninstalled");
            Check(PortableSetup.NeedsSetup(root,data),"removed integration requires setup again");
            Check(File.Exists(Path.Combine(saves,"test.save")),"uninstall preserves saves");
            PortableSetup.Install(root,game,data,Path.Combine(workspace,"No saves yet"),false);
            PortableSetup.Install(root,game,data,saves,true);
            string bridge=File.ReadAllText(Path.Combine(root,"bridge","AfterInit2.tjs"));
            int headerEnd=bridge.IndexOf('\n');
            File.WriteAllText(target,"var vnhOutputPath = \"D:/Earlier helper/work/bridge-state.txt\";\n"+bridge.Substring(headerEnd+1),System.Text.Encoding.Unicode);
            string legacy=PortableSetup.Hash(target);
            Check(PortableSetup.ExistingHash(root,game)==legacy,"recognized legacy helper without manifest");
            Refuses(()=>PortableSetup.Install(root,game,data,saves,false),"legacy replacement needs consent");
            Refuses(()=>PortableSetup.Install(root,game,data,saves,false,hash),"approval bound to exact installed bytes");
            PortableSetup.Install(root,game,data,saves,false,legacy);
            Check(!PortableSetup.NeedsSetup(root,data),"legacy migration adopts portable ownership and data path");
            Check(Directory.GetFiles(Path.Combine(data,"backups"),"AfterInit2.tjs",SearchOption.AllDirectories).Any(f=>PortableSetup.Hash(f)==legacy),"previous legacy bridge backed up exactly");
            PortableSetup.Install(root,game,data,saves,true);
            File.AppendAllText(Path.Combine(game,"fata.exe"),"unknown build");
            Refuses(()=>PortableSetup.Install(root,game,data,saves,false),"unsupported executable refused");
            var paths=PortableSetup.LibraryPaths("\"path\" \"D:\\\\SteamLibrary\"\n\"1\" \"E:\\\\Games\"\n\"2\" \"100\"").ToArray();
            Check(paths.Length==2 && paths[0]==@"D:\SteamLibrary" && paths[1]==@"E:\Games","Steam modern/legacy libraries");
            File.WriteAllText(Path.Combine(workspace,"result.txt"),"PASS: first-run detection; fresh install; confirmed update/removal; cancellation leaves existing files untouched; legacy migration without manifest; exact approval hash and backups; unrelated/modified extension and unsupported build rejection; Steam library parsing.");
            return 0;
        } catch(Exception ex) { File.WriteAllText(Path.Combine(workspace,"result.txt"),ex.ToString()); return 1; }
    }
}
}
