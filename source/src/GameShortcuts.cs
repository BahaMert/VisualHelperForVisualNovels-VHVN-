using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace VisualNovelHelper {
// Pure policy: only configured game-focused keys are consumed. Never stores typed text.
public sealed class ShortcutPolicy {
    int repeatKey, repeatModifiers, toggleKey, toggleModifiers, skipKey, skipModifiers;
    public bool SkipHeld { get; private set; }
    public void ReleaseSkip() { SkipHeld=false; }
    readonly HashSet<int> held=new HashSet<int>();
    readonly HashSet<int> consumed=new HashSet<int>();
    public void Configure(int rk,int rm,int tk,int tm,int sk=0,int sm=0) {
        repeatKey=rk; repeatModifiers=rm; toggleKey=tk; toggleModifiers=tm;
        skipKey=sk; skipModifiers=sm; SkipHeld=false;
        held.Clear(); consumed.Clear();
    }
    public bool Handle(int key,bool down,int modifiers,bool focused,out int action) {
        action=0;
        if(key!=repeatKey && key!=toggleKey && key!=skipKey) return false;
        bool already=held.Contains(key);
        if(down) held.Add(key); else held.Remove(key);
        if(!focused) { SkipHeld=false; consumed.Remove(key); return false; }
        if(!down) { if(key==skipKey) SkipHeld=false; return consumed.Remove(key); }
        if(already) return consumed.Contains(key);
        if(key!=0 && key==skipKey && modifiers==skipModifiers) { SkipHeld=true; consumed.Add(key); return true; }
        if(key!=0 && key==repeatKey && modifiers==repeatModifiers) action=1;
        else if(key!=0 && key==toggleKey && modifiers==toggleModifiers) action=2;
        if(action==0) return false;
        consumed.Add(key); return true;
    }
}
public sealed class GameShortcuts : IDisposable {
    public const int Message=0x8000+73;
    delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
    [StructLayout(LayoutKind.Sequential)] struct KeyData { public uint key,scan,flags,time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct NativeMessage { public IntPtr window; public uint message; public UIntPtr wParam; public IntPtr lParam; public uint time; public int x,y; public uint privateData; }
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int type,HookProc callback,IntPtr module,uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread,uint message,UIntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")] static extern int GetMessage(out NativeMessage message,IntPtr window,uint min,uint max);
    [DllImport("user32.dll")] static extern bool PeekMessage(out NativeMessage message,IntPtr window,uint min,uint max,uint flags);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string module);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    readonly ShortcutPolicy policy=new ShortcutPolicy();
    readonly IntPtr window;
    readonly Thread thread;
    readonly ManualResetEvent ready=new ManualResetEvent(false);
    HookProc callback;
    IntPtr hook;
    uint threadId;
    Exception startupError;
    public volatile int GamePid;
    public volatile bool Suspended;
    public bool SkipHeld { get {
        int mods=(GetAsyncKeyState(18)<0?1:0)|(GetAsyncKeyState(17)<0?2:0)|(GetAsyncKeyState(16)<0?4:0)|((GetAsyncKeyState(91)<0 || GetAsyncKeyState(92)<0)?8:0);
        lock(policy) { if(Suspended || mods!=skipModifiers) policy.ReleaseSkip(); return policy.SkipHeld; }
    } }
    public void ReleaseSkip() { lock(policy) policy.ReleaseSkip(); }
    bool disposed;
    int skipModifiers;
    public GameShortcuts(IntPtr targetWindow) {
        window=targetWindow;
        thread=new Thread(Run) { IsBackground=true,Name="VHVN game shortcuts" };
        thread.Start(); ready.WaitOne();
        if(startupError!=null) { ready.Dispose(); throw startupError; }
    }
    public void Configure(Preferences settings) {
        skipModifiers=settings.SkipModifiers;
        lock(policy) policy.Configure(settings.RepeatKey,settings.RepeatModifiers,settings.ToggleKey,settings.ToggleModifiers,settings.RemapSkip?settings.SkipKey:0,settings.SkipModifiers);
    }
    void Run() {
        try {
            threadId=GetCurrentThreadId(); NativeMessage message;
            PeekMessage(out message,IntPtr.Zero,0,0,0); // Establish the queue before disposal can post WM_QUIT.
            callback=OnKey;
            hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
            if(hook==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not enable game shortcuts.");
            ready.Set();
            while(GetMessage(out message,IntPtr.Zero,0,0)>0) {}
        } catch(Exception ex) { startupError=ex; ready.Set(); }
        finally { if(hook!=IntPtr.Zero) UnhookWindowsHookEx(hook); }
    }
    IntPtr OnKey(int code,IntPtr message,IntPtr pointer) {
        if(code<0) return CallNextHookEx(hook,code,message,pointer);
        try {
            int kind=message.ToInt32();
            if(kind!=0x100 && kind!=0x104 && kind!=0x101 && kind!=0x105) return CallNextHookEx(hook,code,message,pointer);
            var key=(KeyData)Marshal.PtrToStructure(pointer,typeof(KeyData));
            // Ignore modifier-only messages; their asynchronous state has not changed yet.
            if(key.key==16 || key.key==17 || key.key==18 || (key.key>=160 && key.key<=165)) return CallNextHookEx(hook,code,message,pointer);
            uint pid; GetWindowThreadProcessId(GetForegroundWindow(),out pid);
            bool focused=!Suspended && GamePid!=0 && pid==(uint)GamePid;
            int modifiers=0;
            if(focused) {
                if(GetAsyncKeyState(18)<0) modifiers|=1;
                if(GetAsyncKeyState(17)<0) modifiers|=2;
                if(GetAsyncKeyState(16)<0) modifiers|=4;
                if(GetAsyncKeyState(91)<0 || GetAsyncKeyState(92)<0) modifiers|=8;
            }
            int action; bool consume;
            lock(policy) consume=policy.Handle((int)key.key,kind==0x100 || kind==0x104,modifiers,focused,out action);
            if(consume) {
                // Never perform speech, disk I/O or synchronous UI calls on this hook thread.
                if(action!=0 && !PostMessage(window,Message,(IntPtr)action,IntPtr.Zero)) return CallNextHookEx(hook,code,message,pointer);
                return (IntPtr)1;
            }
        } catch { /* Fail open: an error must never block ordinary typing. */ }
        return CallNextHookEx(hook,code,message,pointer);
    }
    public void Dispose() {
        if(disposed) return; disposed=true;
        PostThreadMessage(threadId,0x12,UIntPtr.Zero,IntPtr.Zero);
        if(thread.Join(2000)) ready.Dispose();
    }
}
}
