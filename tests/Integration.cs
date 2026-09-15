using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Reflection;
using Drawing=System.Drawing;
using Forms=System.Windows.Forms;
class Integration {
 static string root=TestPaths.Artifacts;
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static void Render(Controller c,string file){c.Window.UpdateLayout();var image=new RenderTargetBitmap((int)c.Window.ActualWidth,(int)c.Window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(c.Window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var output=File.Create(Path.Combine(root,file)))encoder.Save(output);}
 [STAThread] public static void Main(){Native.SetProcessDpiAwarenessContext(new IntPtr(-4));var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};Controller c=null;IDataObject oldClipboard=null;
  try{oldClipboard=Clipboard.GetDataObject();c=new Controller(false,false);c.AnnotationOverride=image=>new Drawing.Bitmap(image);c.OutputOverride=root;
   c.Window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,(Action)(async()=>{try{
    Render(c,"record-ui.png");c.Mode("shot");Render(c,"screenshot-ui.png");c.Mode("settings");Render(c,"settings-ui.png");c.Mode("record");
    var testWindowHandle=new System.Windows.Interop.WindowInteropHelper(c.Window).Handle;bool canRegister=Native.RegisterHotKey(testWindowHandle,99,0x4003,0x41);if(canRegister)Native.UnregisterHotKey(testWindowHandle,99);File.WriteAllText(Path.Combine(root,"hotkey.txt"),canRegister?"AVAILABLE":"OCCUPIED");
    c.PickerOverride=p=>{var b=Forms.Screen.PrimaryScreen.Bounds;p.Selected=new Drawing.Rectangle(b.Left+220,b.Top+230,321,181);return Forms.DialogResult.OK;};
    await c.Screenshot(true);Assert(c.State=="idle"&&File.Exists(c.LastFile),"Screenshot not saved");using(var bitmap=new Drawing.Bitmap(c.LastFile))Assert(bitmap.Width==321&&bitmap.Height==181,"Screenshot odd size not preserved");Assert(Clipboard.ContainsImage()&&Clipboard.GetImage().PixelWidth==321,"Screenshot clipboard failed");
    string previous=c.LastFile;c.PickerOverride=p=>Forms.DialogResult.Cancel;await c.Screenshot(true);Assert(c.LastFile==previous&&c.State=="idle","Screenshot cancel invalid");
    c.AnnotationOverride=null;c.PickerOverride=p=>{var b=Forms.Screen.PrimaryScreen.Bounds;p.Selected=new Drawing.Rectangle(b.Left+220,b.Top+230,321,181);return Forms.DialogResult.OK;};
    var finishEditor=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
    finishEditor.Tick+=(s,e)=>{var editor=Application.Current.Windows.OfType<AnnotationWindow>().FirstOrDefault();if(editor==null)return;finishEditor.Stop();editor.SetColor(Colors.Red);editor.Ink.Strokes.Add(new System.Windows.Ink.Stroke(new System.Windows.Input.StylusPointCollection{new System.Windows.Input.StylusPoint(15,25),new System.Windows.Input.StylusPoint(100,25)},editor.Ink.DefaultDrawingAttributes.Clone()));editor.Complete();};finishEditor.Start();
    await c.Screenshot(true);using(var painted=new Drawing.Bitmap(c.LastFile)){Assert(painted.Width==321&&painted.Height==181,"Annotated image dimensions changed");Assert(painted.GetPixel(50,25).R>220&&painted.GetPixel(50,25).G<80,"Screenshot annotation was not saved");}
    previous=c.LastFile;var cancelEditor=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};cancelEditor.Tick+=(s,e)=>{var editor=Application.Current.Windows.OfType<AnnotationWindow>().FirstOrDefault();if(editor==null)return;cancelEditor.Stop();editor.DialogResult=false;};cancelEditor.Start();await c.Screenshot(true);Assert(c.LastFile==previous&&c.State=="idle","Annotation cancel saved a file");
    c.UI<CheckBox>("SystemAudio").IsChecked=false;c.UI<CheckBox>("Microphone").IsChecked=false;var pending=c.Begin();await Task.Delay(500);c.Stop();await pending;Assert(c.State=="idle","Countdown cancel failed");
    await c.Begin();await Task.Delay(1700);Assert(c.State=="recording","Record did not start");
    var hud=typeof(Controller).GetField("hud",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c) as Hud;var button=typeof(Hud).GetField("stop",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hud) as Button;button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    for(int n=0;n<80&&c.State!="idle";n++)await Task.Delay(200);Assert(c.State=="idle"&&File.Exists(c.LastFile),"Recording save failed");
    File.WriteAllText(Path.Combine(root,"integration.txt"),"PASS: WPF render; screenshot exact 321x181 PNG; clipboard; cancel; countdown cancel; restart; recording; floating stop; finalize.\n"+c.LastFile);
   }catch(Exception ex){File.WriteAllText(Path.Combine(root,"integration.txt"),ex.ToString());Environment.ExitCode=1;}finally{try{if(oldClipboard!=null)Clipboard.SetDataObject(oldClipboard,true);}catch{}c.RequestQuit();}}));app.Run();
  }catch(Exception ex){File.WriteAllText(Path.Combine(root,"integration.txt"),ex.ToString());Environment.ExitCode=1;}
 }
}
