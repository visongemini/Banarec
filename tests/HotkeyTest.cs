using System;using System.IO;using System.Threading.Tasks;using System.Runtime.InteropServices;using System.Windows;using System.Windows.Controls;using System.Windows.Threading;using System.Windows.Interop;using Forms=System.Windows.Forms;using Drawing=System.Drawing;
class HotkeyTest {
 [StructLayout(LayoutKind.Explicit,Size=40)] struct Input { [FieldOffset(0)]public uint type;[FieldOffset(8)]public ushort key;[FieldOffset(12)]public uint flags; }
 [DllImport("user32.dll")] static extern uint SendInput(uint count,Input[] input,int size);
 static Input K(ushort k,bool up){return new Input{type=1,key=k,flags=up?2u:0u};}
 [STAThread] static void Main(){Native.SetProcessDpiAwarenessContext(new IntPtr(-4));var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var c=new Controller(false,true);c.AnnotationOverride=image=>new Drawing.Bitmap(image);c.OutputOverride=TestPaths.Artifacts;int count=0;var clipboard=Clipboard.GetDataObject();c.PickerOverride=p=>{count++;var b=Forms.Screen.PrimaryScreen.Bounds;p.Selected=new Drawing.Rectangle(b.Left+350,b.Top+350,123,79);return Forms.DialogResult.OK;};
  c.Window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,(Action)(async()=>{try{
   c.Show();await Task.Delay(250);var inputs=new[]{K(0x11,false),K(0x12,false),K(0x41,false),K(0x41,true),K(0x12,true),K(0x11,true)};
   if(SendInput((uint)inputs.Length,inputs,40)!=inputs.Length)throw new Exception("SendInput failed");
   for(int i=0;i<40&&(count==0||c.State!="idle");i++)await Task.Delay(100);
   if(count!=1||!File.Exists(c.LastFile))throw new Exception("Global shortcut failed: count="+count+" state="+c.State);
   var test=new Window{Title="Banarec shortcut test",Width=320,Height=100};var text=new TextBox{Text="Banarec test"};test.Content=text;test.Show();test.Activate();text.Focus();await Task.Delay(150);
   var select=new[]{K(0x11,false),K(0x41,false),K(0x41,true),K(0x11,true)};SendInput((uint)select.Length,select,40);await Task.Delay(250);
   if(text.SelectedText!="Banarec test"||count!=1)throw new Exception("Ctrl+A regression");test.Close();
   File.WriteAllText(Path.Combine(TestPaths.Artifacts,"keyboard.txt"),"PASS: actual Ctrl+Alt+A triggers one screenshot despite registration conflict; Ctrl+A remains Select All.");
  }catch(Exception ex){File.WriteAllText(Path.Combine(TestPaths.Artifacts,"keyboard.txt"),ex.ToString());Environment.ExitCode=1;}finally{try{Clipboard.SetDataObject(clipboard,true);}catch{}c.RequestQuit();}}));app.Run();
 }
}
