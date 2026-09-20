using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

class VoicePreview {
 [STAThread] static void Main() {
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  app.Dispatcher.BeginInvoke(new Action(()=>{
   Window window=null;
   try {
    using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Main.xaml"))window=(Window)XamlReader.Load(stream);
    foreach(string name in new[]{"RecordPane","ShotPane","SettingsPane"})((FrameworkElement)window.FindName(name)).Visibility=Visibility.Collapsed;
    ((FrameworkElement)window.FindName("VoicePane")).Visibility=Visibility.Visible;
    ((RadioButton)window.FindName("RecordTab")).IsChecked=false;((RadioButton)window.FindName("VoiceTab")).IsChecked=true;
    ((TextBlock)window.FindName("VoiceServiceStatus")).Text="极速识别已配置";
    ((ComboBox)window.FindName("VoiceBackend")).SelectedIndex=1;
    ((ComboBox)window.FindName("VoiceBehavior")).SelectedIndex=0;
    ((TextBlock)window.FindName("VoiceTranscript")).Text="琵琶雅礼今天卖了 3 单香氛礼盒。";
    ((ProgressBar)window.FindName("VoiceLevel")).Value=0.64;
    ((TextBlock)window.FindName("VoiceTermCount")).Text="2 个启用 · 前约 100 tokens 生效";
    window.Show();window.UpdateLayout();
    var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);
    var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(TestPaths.Artifacts,"voice-page.png")))png.Save(file);
    File.WriteAllText(Path.Combine(TestPaths.Artifacts,"voice-preview.txt"),"PASS: loaded Main.xaml and rendered the isolated voice page without Controller, hooks, microphone, clipboard, or desktop capture.");
   } catch(Exception error){Directory.CreateDirectory(TestPaths.Artifacts);File.WriteAllText(Path.Combine(TestPaths.Artifacts,"voice-preview.txt"),error.ToString());Environment.ExitCode=1;}
   finally {if(window!=null)window.Close();app.Shutdown();}
  }),DispatcherPriority.ApplicationIdle);
  app.Run();
 }
}
