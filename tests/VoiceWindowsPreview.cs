using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

class VoiceWindowsPreview {
 static ResourceDictionary LoadMainResources() {
  string xaml=Path.GetFullPath(Path.Combine(TestPaths.Artifacts,"..","..","src","Main.xaml"));
  using(var stream=File.OpenRead(xaml)){
   var source=(Window)XamlReader.Load(stream);var resources=source.Resources;source.Resources=new ResourceDictionary();source.Close();return resources;
  }
 }
 static T FindVisual<T>(DependencyObject root) where T:DependencyObject {
  var typed=root as T;if(typed!=null)return typed;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindVisual<T>(VisualTreeHelper.GetChild(root,i));if(found!=null)return found;}return null;
 }
 static void Render(Window window,string name) {
  window.Show();window.UpdateLayout();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(TestPaths.Artifacts,name)))png.Save(file);
 }
 [STAThread] static void Main() {
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  app.Dispatcher.BeginInvoke(new Action(()=>{
   VoiceHud hud=null;VoiceLibraryWindow library=null;
   try {
    hud=new VoiceHud(LoadMainResources());hud.Update(VoiceState.Listening,"琵琶雅礼今天卖了 3 单香氛礼盒。",0.72,TimeSpan.FromSeconds(8));Render(hud,"voice-hud.png");hud.Hide();
    var terms=new List<VoiceTerm>{VoiceData.NewTerm("琵琶雅礼",new List<string>{"琵琶牙里"},"correction"),VoiceData.NewTerm("BanaStudio",new List<string>{"Banarec"},"manual")};
    var history=new List<VoiceHistoryItem>{new VoiceHistoryItem{id="one",createdAt=DateTime.UtcNow.ToString("o"),text="琵琶雅礼今天卖了 3 单香氛礼盒。",status="final"},new VoiceHistoryItem{id="two",createdAt=DateTime.UtcNow.AddMinutes(-8).ToString("o"),text="一段尚未确认的草稿",status="incomplete"}};
    library=new VoiceLibraryWindow(LoadMainResources(),terms,history);Render(library,"voice-library.png");var tabs=FindVisual<TabControl>(library);if(tabs==null||tabs.Items.Count<2)throw new Exception("history tab was not found");tabs.SelectedIndex=1;Render(library,"voice-history.png");
    if(library.Terms.Count!=2||library.History.Count!=2)throw new Exception("library input collections were not retained");
    File.WriteAllText(Path.Combine(TestPaths.Artifacts,"voice-windows.txt"),"PASS: rendered no-activate voice HUD, vocabulary tab, and history tab with synthetic data and the real Window.Resources loaded from src/Main.xaml; no persistence actions invoked.");
   }catch(Exception error){Directory.CreateDirectory(TestPaths.Artifacts);File.WriteAllText(Path.Combine(TestPaths.Artifacts,"voice-windows.txt"),error.ToString());Environment.ExitCode=1;}
   finally{if(library!=null)library.Close();if(hud!=null)hud.Dispose();app.Shutdown();}
  }),DispatcherPriority.ApplicationIdle);app.Run();
 }
}
