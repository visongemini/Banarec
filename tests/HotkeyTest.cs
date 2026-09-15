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
   var captureTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};captureTimer.Tick+=(s,e)=>{captureTimer.Stop();var p=new[]{K(0x11,false),K(0x12,false),K(0x50,false),K(0x50,true),K(0x12,true),K(0x11,true)};SendInput((uint)p.Length,p,40);};captureTimer.Start();c.UI<Button>("ScreenshotShortcutButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if((string)c.UI<Button>("ScreenshotShortcutButton").Content!="Ctrl + Alt + P")throw new Exception("Shortcut capture UI failed");
   var capturedShot=new[]{K(0x11,false),K(0x12,false),K(0x50,false),K(0x50,true),K(0x12,true),K(0x11,true)};SendInput((uint)capturedShot.Length,capturedShot,40);for(int i=0;i<40&&(count<2||c.State!="idle");i++)await Task.Delay(100);if(count!=2)throw new Exception("Captured screenshot shortcut failed");
   c.SetHotkeysForTest(new HotkeyGesture(HotkeyGesture.Control|HotkeyGesture.Alt,0x42),new HotkeyGesture(HotkeyGesture.Control|HotkeyGesture.Shift,0x52));await Task.Delay(150);
   SendInput((uint)inputs.Length,inputs,40);await Task.Delay(350);if(count!=2)throw new Exception("Old screenshot shortcut remained active");
   var customShot=new[]{K(0x11,false),K(0x12,false),K(0x42,false),K(0x42,true),K(0x12,true),K(0x11,true)};SendInput((uint)customShot.Length,customShot,40);for(int i=0;i<40&&(count<3||c.State!="idle");i++)await Task.Delay(100);if(count!=3)throw new Exception("Custom screenshot shortcut failed");
   var customRecord=new[]{K(0x11,false),K(0x10,false),K(0x52,false),K(0x52,true),K(0x10,true),K(0x11,true)};SendInput((uint)customRecord.Length,customRecord,40);for(int i=0;i<20&&c.State=="idle";i++)await Task.Delay(50);if(c.State!="countdown")throw new Exception("Custom recording shortcut failed: "+c.State);SendInput((uint)customRecord.Length,customRecord,40);for(int i=0;i<20&&c.State!="idle";i++)await Task.Delay(50);if(c.State!="idle")throw new Exception("Custom recording stop shortcut failed: "+c.State);
   var settings=File.ReadAllLines(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Banarec","settings.ini"));if(settings.Length<9||settings[5]!="3"||settings[6]!="66"||settings[7]!="6"||settings[8]!="82")throw new Exception("Custom shortcuts were not persisted");
   var test=new Window{Title="Banarec shortcut test",Width=320,Height=100};var text=new TextBox{Text="Banarec test"};test.Content=text;test.Show();test.Activate();text.Focus();await Task.Delay(150);
   var select=new[]{K(0x11,false),K(0x41,false),K(0x41,true),K(0x11,true)};SendInput((uint)select.Length,select,40);await Task.Delay(250);
   if(text.SelectedText!="Banarec test"||count!=3)throw new Exception("Ctrl+A regression: selected='"+text.SelectedText+"' count="+count+" modifiers="+System.Windows.Input.Keyboard.Modifiers);test.Close();
   File.WriteAllText(Path.Combine(TestPaths.Artifacts,"keyboard.txt"),"PASS: shortcut capture UI, defaults, custom Ctrl+Alt+B screenshot, custom Ctrl+Shift+R record toggle, persistence, and Ctrl+A isolation.");
  }catch(Exception ex){File.WriteAllText(Path.Combine(TestPaths.Artifacts,"keyboard.txt"),ex.ToString());Environment.ExitCode=1;}finally{try{Clipboard.SetDataObject(clipboard,true);}catch{}c.RequestQuit();}}));app.Run();
 }
}
