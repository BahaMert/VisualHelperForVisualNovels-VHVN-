using System;
namespace VisualNovelHelper {
public static class ShortcutTests {
    static void Check(bool value,string name) { if(!value) throw new Exception("Shortcut: "+name); }
    public static void Run() {
        var p=new ShortcutPolicy(); p.Configure(82,0,84,2); int action;
        Check(!p.Handle(82,true,0,false,out action) && action==0,"typing R in another app passes through");
        Check(!p.Handle(82,false,0,false,out action),"outside key release passes through");
        Check(p.Handle(82,true,0,true,out action) && action==1,"R repeats in game");
        Check(p.Handle(82,true,0,true,out action) && action==0,"held R does not repeat continuously");
        Check(!p.Handle(82,true,0,false,out action) && action==0,"immediate focus loss passes repeat through");
        Check(!p.Handle(82,false,0,false,out action),"keyup after focus loss passes through");
        Check(!p.Handle(84,true,0,true,out action),"T without Ctrl is ordinary game input");
        p.Handle(84,false,0,true,out action);
        Check(p.Handle(84,true,2,true,out action) && action==2,"Ctrl+T toggles in game");
        Check(p.Handle(84,false,0,true,out action),"release consumed key after modifier released");
        Check(!p.Handle(84,true,2,false,out action),"Ctrl+T works outside game");
        Check(!p.Handle(84,true,2,true,out action),"holding outside key while switching to game does not trigger");
        p.Handle(84,false,2,true,out action);
        Check(!p.Handle(82,true,8,true,out action),"Windows shortcut does not match plain R");
        p.Handle(82,false,8,true,out action);
        p.Configure(0,0,0,0);
        Check(!p.Handle(82,true,0,true,out action),"unassigned passes through");
        p.Configure(82,0,84,0,20,0);
        Check(p.Handle(20,true,0,true,out action) && p.SkipHeld && action==0,"Caps Lock held without changing toggle state");
        Check(p.Handle(20,true,0,true,out action) && p.SkipHeld,"skip autorepeat remains held");
        Check(p.Handle(20,false,0,true,out action) && !p.SkipHeld,"release stops skip");
        Check(!p.Handle(17,true,0,true,out action) && !p.Handle(18,true,2,true,out action),"Ctrl and Alt never consumed by skip");
        Check(!p.Handle(20,true,0,false,out action) && !p.SkipHeld,"Caps Lock outside game passes through");
        Check(!p.Handle(20,true,0,true,out action) && !p.SkipHeld,"outside-held key cannot start skipping on focus return");
        p.Handle(20,false,0,true,out action);
        p.Configure(82,0,84,0,120,4);
        Check(!p.Handle(120,true,0,true,out action),"configured skip modifier required"); p.Handle(120,false,0,true,out action);
        Check(p.Handle(120,true,4,true,out action) && p.SkipHeld,"custom skip combo");
        p.ReleaseSkip(); Check(!p.SkipHeld,"focus loss releases hold without another keyboard event");
        p.Configure(82,0,84,0); Check(!p.Handle(20,true,0,true,out action),"disabling remap restores normal Caps Lock");
        // Exercise creation and clean shutdown of the actual native message-loop thread.
        using(var native=new GameShortcuts(IntPtr.Zero)) { native.Configure(new Preferences {RepeatKey=82}); }
    }
}
}
