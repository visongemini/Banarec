using System;
using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing=System.Drawing;
class AnnotationTest {
 static void Check(bool test,string text){if(!test)throw new Exception(text);}
 [STAThread] static void Main(){Native.SetProcessDpiAwarenessContext(new IntPtr(-4));var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var theme=Entry.LoadWindow("Main.xaml");
  using(var image=new Drawing.Bitmap(641,361)){using(var g=Drawing.Graphics.FromImage(image))g.Clear(Drawing.Color.White);var editor=new AnnotationWindow(image,theme.Resources);editor.Show();editor.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,(Action)(()=>{try{
   editor.SetTool("pen");editor.SetColor(Colors.Red);var points=new StylusPointCollection{new StylusPoint(50,50),new StylusPoint(200,50)};editor.Ink.Strokes.Add(new Stroke(points,editor.Ink.DefaultDrawingAttributes.Clone()));
   using(var rendered=editor.Export()){Check(rendered.Width==641&&rendered.Height==361,"Original dimensions changed");var pixel=rendered.GetPixel(100,50);Check(pixel.R>220&&pixel.G<80,"Pen not rendered");}
   editor.Undo();Check(editor.Ink.Strokes.Count==0,"Undo failed");editor.Redo();Check(editor.Ink.Strokes.Count==1,"Redo failed");
   editor.SetTool("highlight");Check(editor.Ink.DefaultDrawingAttributes.IsHighlighter,"Highlighter style missing");editor.Ink.Strokes.Add(new Stroke(new StylusPointCollection{new StylusPoint(60,100),new StylusPoint(250,100)},editor.Ink.DefaultDrawingAttributes.Clone()));
   editor.SetTool("erase");Check(editor.Ink.EditingMode==System.Windows.Controls.InkCanvasEditingMode.EraseByStroke,"Eraser mode missing");editor.Ink.Strokes.RemoveAt(0);editor.Undo();Check(editor.Ink.Strokes.Count==2,"Erase undo failed");
   using(var rendered=editor.Export()){rendered.Save(Path.Combine(TestPaths.Artifacts,"annotation-export.png"),Drawing.Imaging.ImageFormat.Png);Check(rendered.GetPixel(150,100).B<245,"Highlighter not exported");}
   editor.UpdateLayout();var bitmap=new RenderTargetBitmap((int)editor.ActualWidth,(int)editor.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(editor);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(TestPaths.Artifacts,"annotation-ui.png")))png.Save(f);
   File.WriteAllText(Path.Combine(TestPaths.Artifacts,"annotation.txt"),"PASS: pen color, highlighter, eraser, undo/redo, original 641x361 export, composited PNG.");
  }catch(Exception ex){File.WriteAllText(Path.Combine(TestPaths.Artifacts,"annotation.txt"),ex.ToString());Environment.ExitCode=1;}finally{editor.Close();app.Shutdown();}}));app.Run();}
 }
}
