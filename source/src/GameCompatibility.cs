using System;
using System.IO;
namespace VisualNovelHelper {
public sealed class CompatibilityResult {
    public string Hash;
    public bool VerifiedFallback;
}
public static class GameCompatibility {
    // This is a compatibility heuristic, not an assertion that every patch works.
    // Modified builds must establish a live structured bridge; no fixed-address fallback.
    public static CompatibilityResult Inspect(string executable,GameProfile profile) {
        if(!File.Exists(executable)) throw new InvalidOperationException("Choose the game folder containing fata.exe.");
        string hash=PortableSetup.Hash(executable);
        if(String.Equals(hash,profile.exeSha256,StringComparison.OrdinalIgnoreCase))
            return new CompatibilityResult {Hash=hash,VerifiedFallback=true};
        string folder=Path.GetDirectoryName(executable);
        if(!IsX86Executable(executable)) throw new InvalidOperationException("This executable is not a supported 32-bit Windows game. Select the English Steam game's fata.exe folder.");
        if(!IsArchive(Path.Combine(folder,"data.xp3")) || !IsArchive(Path.Combine(folder,"data_en.xp3")))
            throw new InvalidOperationException("The English game data could not be identified. Choose the folder containing fata.exe, data.xp3 and data_en.xp3.");
        return new CompatibilityResult {Hash=hash,VerifiedFallback=false};
    }
    static bool IsX86Executable(string file) {
        try {
            using(var stream=File.OpenRead(file)) using(var reader=new BinaryReader(stream)) {
                if(stream.Length<64 || reader.ReadUInt16()!=0x5a4d) return false;
                stream.Position=0x3c; int offset=reader.ReadInt32();
                if(offset<64 || offset>stream.Length-26) return false;
                stream.Position=offset;
                if(reader.ReadUInt32()!=0x4550 || reader.ReadUInt16()!=0x14c) return false;
                ushort sections=reader.ReadUInt16(); if(sections==0 || sections>96) return false;
                stream.Position=offset+20; ushort optionalSize=reader.ReadUInt16(), flags=reader.ReadUInt16();
                if(optionalSize<96 || (long)offset+24+optionalSize+(long)sections*40>stream.Length) return false;
                return (flags&2)!=0 && (flags&0x2000)==0 && reader.ReadUInt16()==0x10b;
            }
        } catch(EndOfStreamException) { return false; }
    }
    static bool IsArchive(string file) {
        if(!File.Exists(file)) return false;
        byte[] signature={0x58,0x50,0x33,0x0d,0x0a,0x20,0x0a,0x1a,0x8b,0x67,0x01};
        using(var stream=File.OpenRead(file)) {
            if(stream.Length<19) return false;
            foreach(byte expected in signature) if(stream.ReadByte()!=expected) return false;
            return true;
        }
    }
}
public sealed class BridgeConnectionStatus {
    readonly DateTime started;
    int state;
    public BridgeConnectionStatus(DateTime now) { started=now; }
    public string Update(bool live,DateTime now) {
        int next=live?1:((now-started).TotalSeconds>=15?2:0);
        if(next==state) return null; state=next;
        if(next==1) return "Connected in compatibility mode. Return to the game and advance normally.";
        if(next==2) return "Game text is not responding. Restart the game after setup. If this continues, this game modification may need an adapter update.";
        return null;
    }
}
}
