using System;
using System.IO;
using System.Text;
namespace VisualNovelHelper {
// Short-lived, session-bound input state. No global Ctrl/Alt interception or injected keys.
public sealed class GameInputChannel {
    readonly string path;
    long sequence;
    DateTime next;
    string previous;
    public GameInputChannel(string statePath) { path=statePath+".input"; }
    public void Publish(string session,bool remap,int key,bool held,DateTime now) {
        if(String.IsNullOrEmpty(session)) return;
        string state=session+"\n"+(remap?"1":"0")+"\n"+key+"\n"+(held?"1":"0");
        if(state==previous && now<next) return;
        sequence++;
        File.WriteAllText(path,"VNHI1\n"+sequence+"\n"+state+"\nEND\t"+sequence+"\n",Encoding.Unicode);
        previous=state; next=now.AddMilliseconds(250);
    }
}
}
