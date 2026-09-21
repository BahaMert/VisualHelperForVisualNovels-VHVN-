using System;
using System.Threading.Tasks;
namespace VisualNovelHelper {
public static class OpeningTests {
    static void Check(bool value,string name) {if(!value) throw new Exception("Opening: "+name);}
    public static void Run() {
        var first=new TaskCompletionSource<OpeningResult>(); var second=new TaskCompletionSource<OpeningResult>(); int calls=0;
        var reader=new OpeningReader(slot=>++calls==1?first.Task:second.Task);
        Check(reader.Update("a","0",true)==null && calls==1,"one recognition starts");
        reader.Update("b","1",true); Check(calls==1,"no concurrent OCR queue");
        first.SetResult(new OpeningResult {Text="Old card"});
        Check(reader.Update("b","1",true)==null && calls==2,"stale card discarded, latest starts");
        second.SetResult(new OpeningResult {Text="Current card"});
        Check(reader.Update("b","1",true)=="Current card","current result speaks");
        Check(reader.Update("b","1",true)==null && calls==2,"card never repeats each tick");
        var lost=new TaskCompletionSource<OpeningResult>(); reader=new OpeningReader(slot=>lost.Task);
        reader.Update("c","0",true); reader.Update("c","0",false);
        lost.SetResult(new OpeningResult {Text="Do not speak after focus loss"});
        Check(reader.Update("c","0",true)==null,"focus loss invalidates pending audio");
        var left=new TaskCompletionSource<OpeningResult>(); reader=new OpeningReader(slot=>left.Task);
        reader.Update("e","0",true); reader.Update(null,null,true);
        left.SetResult(new OpeningResult {Text="Do not speak over normal dialogue"});
        Check(reader.Update(null,null,true)==null,"leaving opening discards pending result even while game remains focused");
        var disabled=new OpeningReader(slot=>{throw new Exception("Should not start");});
        Check(disabled.Update("d","0",false)==null,"disabled setting does not run OCR");
        Check(OpeningReader.Recognize("unused","../../escape").GetAwaiter().GetResult().Error!=null,"only fixed image slots accepted");
    }
}
}
