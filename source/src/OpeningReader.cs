using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
namespace VisualNovelHelper {
public sealed class OpeningResult { public string Text, Error; }
// At most one OCR job; late results never speak over a new scene or another application.
public sealed class OpeningReader {
    readonly Func<string,Task<OpeningResult>> recognize;
    Task<OpeningResult> pending;
    string requested, attempted;
    public string Error { get; private set; }
    public OpeningReader(Func<string,Task<OpeningResult>> loader) { recognize=loader; }
    public string Update(string identity,string slot,bool active) {
        Error=null;
        if(pending!=null && (!active || requested!=identity)) requested=null;
        if(!active) attempted=identity;
        string text=null;
        if(pending!=null && pending.IsCompleted) {
            var result=pending.GetAwaiter().GetResult(); pending=null;
            if(active && identity!=null && requested!=null && requested==identity) {text=result.Text;Error=result.Error;}
        }
        if(active && identity!=null && attempted!=identity && pending==null) {
            attempted=requested=identity; pending=recognize(slot);
        }
        return text;
    }
    public static async Task<OpeningResult> Recognize(string statePath,string slot) {
        if(slot!="0" && slot!="1") return new OpeningResult {Error="Invalid opening image."};
        string snapshot=Path.Combine(Path.GetTempPath(),"vnh-opening-"+Guid.NewGuid().ToString("N")+".bmp");
        string prepared=snapshot+".png";
        try {
            File.Copy(statePath+".opening-"+slot+".bmp",snapshot);
            OpeningImage.Prepare(snapshot,prepared);
            var lines=await ScreenReader.RecognizeFile(prepared);
            return new OpeningResult {Text=String.Join(" ",lines.Select(l=>l.Text)).Trim()};
        } catch(Exception ex) {return new OpeningResult {Error="Opening text could not be read: "+ex.Message};}
        finally {try {File.Delete(snapshot);File.Delete(prepared);} catch(IOException) {}}
    }
}
}
