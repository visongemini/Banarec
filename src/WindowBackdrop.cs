using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

static class WindowBackdrop {
 [StructLayout(LayoutKind.Sequential)] struct AccentPolicy {public int State;public int Flags;public uint Color;public int Animation;}
 [StructLayout(LayoutKind.Sequential)] struct AttributeData {public int Attribute;public IntPtr Data;public int Size;}
 [StructLayout(LayoutKind.Sequential)] struct Margins {public int Left,Right,Top,Bottom;}
 [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr hwnd,ref AttributeData data);
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd,ref Margins margins);
 public static void Enable(Window window){
  try{IntPtr hwnd=new WindowInteropHelper(window).Handle;if(hwnd==IntPtr.Zero)return;var margins=new Margins{Left=-1,Right=-1,Top=-1,Bottom=-1};DwmExtendFrameIntoClientArea(hwnd,ref margins);
   int corner=2;DwmSetWindowAttribute(hwnd,33,ref corner,4);
   if(Environment.OSVersion.Version.Build>=22000){int backdrop=3;DwmSetWindowAttribute(hwnd,38,ref backdrop,4);}else EnableAcrylic(hwnd);
  }catch{}
 }
 static void EnableAcrylic(IntPtr hwnd){var accent=new AccentPolicy{State=4,Flags=2,Color=0xCCF5F5F5,Animation=0};int size=Marshal.SizeOf(accent);IntPtr memory=Marshal.AllocHGlobal(size);try{Marshal.StructureToPtr(accent,memory,false);var data=new AttributeData{Attribute=19,Data=memory,Size=size};SetWindowCompositionAttribute(hwnd,ref data);}finally{Marshal.FreeHGlobal(memory);}}
}
