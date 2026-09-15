using System;
using System.Runtime.InteropServices;

// Used only when RegisterHotKey reports a conflict. No typed text is collected,
// stored, or transmitted. Every input except this exact shortcut passes through.
sealed class ScreenshotShortcut : IDisposable {
 delegate IntPtr Callback(int code,IntPtr message,IntPtr data);
 [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,Callback callback,IntPtr module,uint thread);
 [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
 [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
 readonly Callback callback;readonly Action trigger;IntPtr hook;bool held;
 public bool Active {get{return hook!=IntPtr.Zero;}}
 public ScreenshotShortcut(Action trigger){this.trigger=trigger;callback=Handle;hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);}
 static bool Down(int key){return (GetAsyncKeyState(key)&0x8000)!=0;}
 IntPtr Handle(int code,IntPtr message,IntPtr data){
  if(code>=0&&Marshal.ReadInt32(data)==0x41){int msg=message.ToInt32();bool down=msg==0x100||msg==0x104;bool up=msg==0x101||msg==0x105;
   if(up&&held){held=false;return new IntPtr(1);}
   if(down&&(held||(Down(0x11)&&Down(0x12)&&!Down(0x10)&&!Down(0x5B)&&!Down(0x5C)))){if(!held){held=true;trigger();}return new IntPtr(1);}
  }
  return CallNextHookEx(hook,code,message,data);
 }
 public void Dispose(){if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}}
}
