using System;
using System.IO;
using System.Web.Script.Serialization;
namespace VisualNovelHelper {
public static class CompatibilityTests {
    static void Check(bool value,string name) { if(!value) throw new Exception("Compatibility: "+name); }
    static void Refuses(Action action,string name) {
        bool refused=false; try { action(); } catch(InvalidOperationException) { refused=true; }
        Check(refused,name);
    }
    public static void Run(string root,string game,string data,string saves) {
        var profile=new JavaScriptSerializer().Deserialize<GameProfile>(File.ReadAllText(Path.Combine(root,"profiles","fata.json")));
        string exe=Path.Combine(game,"fata.exe"); byte[] original=File.ReadAllBytes(exe);
        Check(GameCompatibility.Inspect(exe,profile).VerifiedFallback,"verified original retains fallback");
        File.AppendAllText(exe,"invented resource or overlay change");
        Refuses(()=>GameCompatibility.Inspect(exe,profile),"modified build without game data rejected");
        // Invented archive-header fixtures; no game story/assets are copied.
        byte[] archive={0x58,0x50,0x33,0x0d,0x0a,0x20,0x0a,0x1a,0x8b,0x67,0x01,0,0,0,0,0,0,0,0};
        File.WriteAllBytes(Path.Combine(game,"data.xp3"),archive);
        File.WriteAllBytes(Path.Combine(game,"data_en.xp3"),archive);
        Check(!GameCompatibility.Inspect(exe,profile).VerifiedFallback,"appended changes accepted without native fallback");
        PortableSetup.Install(root,game,data,saves,false);
        PortableSetup.Install(root,game,data,saves,true);
        byte[] patched=(byte[])original.Clone(); int pe=BitConverter.ToInt32(patched,0x3c);
        patched[pe+22]^=0x20; // A large-address-aware flag change must not require an identical file hash.
        File.WriteAllBytes(exe,patched);
        Check(!GameCompatibility.Inspect(exe,profile).VerifiedFallback,"header compatibility patch accepted in bridge-only mode");
        PortableSetup.Validate(root,game);
        PortableSetup.Install(root,game,data,saves,false);
        File.WriteAllText(exe,"an unsupported replacement engine");
        PortableSetup.Install(root,game,data,saves,true);
        Check(!File.Exists(Path.Combine(game,"AfterInit2.tjs")),"owned integration can be removed after an incompatible engine change");
        File.WriteAllBytes(exe,patched);
        File.WriteAllText(Path.Combine(game,"data_en.xp3"),"not an archive");
        Refuses(()=>GameCompatibility.Inspect(exe,profile),"invalid English archive rejected");
        File.WriteAllBytes(Path.Combine(game,"data_en.xp3"),archive);
        patched[pe+4]=0x64; patched[pe+5]=0x86; File.WriteAllBytes(exe,patched);
        Refuses(()=>GameCompatibility.Inspect(exe,profile),"changed architecture rejected");
        File.WriteAllText(exe,"not an executable");
        Refuses(()=>GameCompatibility.Inspect(exe,profile),"malformed executable rejected");
        File.WriteAllBytes(exe,original);
    }
    public static void StatusTests() {
        var now=DateTime.UtcNow; var status=new BridgeConnectionStatus(now);
        Check(status.Update(false,now.AddSeconds(1))==null,"does not claim connected before live bridge");
        Check(status.Update(false,now.AddSeconds(16)).Contains("not responding"),"missing bridge gets actionable status");
        Check(status.Update(false,now.AddSeconds(17))==null,"missing bridge does not spam status");
        Check(status.Update(true,now.AddSeconds(20)).StartsWith("Connected"),"late bridge recovers");
        Check(status.Update(false,now.AddSeconds(25)).Contains("not responding"),"lost bridge is reported");
    }
}
}
