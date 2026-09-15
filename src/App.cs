using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;

[assembly:AssemblyTitle("Banarec · 香蕉录屏")]
[assembly:AssemblyProduct("Banarec")]
[assembly:AssemblyVersion("2.1.0.0")]
[assembly:AssemblyFileVersion("2.1.0.0")]

static class Entry {
 [STAThread] public static void Main(string[] args) {
  try {Native.SetProcessDpiAwarenessContext(new IntPtr(-4));}catch{}
  bool owned;using(var mutex=new Mutex(true,"Local\\Banarec.Desktop",out owned)) {
   if(!owned){if(!args.Contains("--startup"))using(var ev=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\Banarec.Show"))ev.Set();return;}
   var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
   app.DispatcherUnhandledException+=(s,e)=>{Log(e.Exception);MessageBox.Show("发生错误："+e.Exception.Message,"Banarec");e.Handled=true;};
   try {var controller=new Controller(args.Contains("--startup"),true);app.Run();GC.KeepAlive(controller);}catch(Exception ex){Log(ex);MessageBox.Show("启动失败："+ex.Message,"Banarec");}
  }
 }
 public static string BaseDir {get{return AppDomain.CurrentDomain.BaseDirectory;}}
 public static void Log(Exception ex){try{string d=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Banarec");Directory.CreateDirectory(d);File.AppendAllText(Path.Combine(d,"errors.log"),DateTime.Now+" "+ex+Environment.NewLine);}catch{}}
 public static Window LoadWindow(string resource){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))return (Window)XamlReader.Load(s);}
}

static class Startup {
 const string Key="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
 public static bool Enabled {get{using(var k=Registry.CurrentUser.OpenSubKey(Key))return k!=null&&k.GetValue("Banarec")!=null;}}
 public static void Set(bool enabled){using(var k=Registry.CurrentUser.CreateSubKey(Key)){if(enabled)k.SetValue("Banarec","\""+Path.Combine(Entry.BaseDir,"Banarec.exe")+"\" --startup");else k.DeleteValue("Banarec",false);}}
}

sealed class Controller {
 public readonly Window Window;
 readonly bool realHotkeys;bool loading=true,quit,disposed,recordKey,shotKey,showAfterShot;
 readonly Forms.NotifyIcon tray;readonly DispatcherTimer timer;readonly EventWaitHandle showEvent;readonly RegisteredWaitHandle showWait;
 ScreenshotShortcut shortcut;
 IntPtr handle;Capture capture;RecordingBorder border;Hud hud;Countdown countdown;RegionPicker picker;
 Forms.Screen screen;Drawing.Rectangle area;int generation;readonly Stopwatch elapsed=new Stopwatch();
 public string State {get;private set;} public string LastFile {get;private set;}
 public Func<Drawing.Bitmap,Drawing.Bitmap> AnnotationOverride=null;
 public string OutputOverride=null;public Func<RegionPicker,Forms.DialogResult> PickerOverride=null;
 readonly string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Banarec","settings.ini");
 public T UI<T>(string name) where T:class{return Window.FindName(name) as T;}
 bool IsOn(string n){return UI<CheckBox>(n).IsChecked==true;}
 void Status(string text){UI<TextBlock>("Status").Text=text;}
 public Controller(bool startup,bool enableHotkeys) {
  realHotkeys=enableHotkeys;State="idle";Window=Entry.LoadWindow("Main.xaml");
  var iconPath=Path.Combine(Entry.BaseDir,"Banarec.png");if(File.Exists(iconPath)){var img=new BitmapImage(new Uri(iconPath));UI<Image>("BrandIcon").Source=img;Window.Icon=img;}
  UI<Grid>("Header").MouseLeftButtonDown+=(s,e)=>{if(e.OriginalSource is TextBlock||e.OriginalSource is Grid||e.OriginalSource is Image){try{Window.DragMove();}catch{}}};
  Bind("MinButton",()=>Window.WindowState=WindowState.Minimized);Bind("CloseButton",()=>Window.Hide());
  Bind("SettingsButton",()=>Mode("settings"));Bind("BackButton",()=>Mode(UI<RadioButton>("ScreenshotTab").IsChecked==true?"shot":"record"));
  UI<RadioButton>("RecordTab").Checked+=(s,e)=>Mode("record");UI<RadioButton>("ScreenshotTab").Checked+=(s,e)=>Mode("shot");
  Bind("FullButton",()=>{area=Capture.Normalize(screen.Bounds,screen.Bounds);RefreshRegion();});Bind("RegionButton",async()=>await PickRegion());
  Bind("StartButton",async()=>{if(State=="idle")await Begin();else Stop();});Bind("ScreenshotButton",async()=>await Screenshot(true));Bind("FolderButton",OpenFolder);
  var screens=UI<ComboBox>("Screens");var all=Forms.Screen.AllScreens;for(int i=0;i<all.Length;i++)screens.Items.Add("显示器 "+(i+1)+(all[i].Primary?" · 主屏":"")+"  "+all[i].Bounds.Width+" × "+all[i].Bounds.Height);
  screens.SelectionChanged+=(s,e)=>{if(screens.SelectedIndex<0)return;screen=all[screens.SelectedIndex];area=Capture.Normalize(screen.Bounds,screen.Bounds);RefreshRegion();};screens.SelectedIndex=Array.FindIndex(all,s=>s.Primary);
  UI<ComboBox>("Framerate").SelectedIndex=0;LoadSettings();UI<CheckBox>("AutoStart").IsChecked=Startup.Enabled;
  foreach(string name in new[]{"SystemAudio","Microphone","Compatibility","ScreenshotHotkey"}){var box=UI<CheckBox>(name);box.Checked+=(s,e)=>SettingsChanged();box.Unchecked+=(s,e)=>SettingsChanged();}
  UI<ComboBox>("Framerate").SelectionChanged+=(s,e)=>SettingsChanged();
  UI<CheckBox>("AutoStart").Checked+=(s,e)=>ChangeStartup(true);UI<CheckBox>("AutoStart").Unchecked+=(s,e)=>ChangeStartup(false);
  loading=false;UpdateQuality();
  var menu=new Forms.ContextMenuStrip();menu.Items.Add("打开 Banarec",null,(s,e)=>Dispatch(Show));menu.Items.Add("截图   Ctrl + Alt + A",null,(s,e)=>Dispatch(async()=>await Screenshot(false)));menu.Items.Add("开始 / 停止录制",null,(s,e)=>Dispatch(async()=>{if(State=="idle")await Begin();else Stop();}));menu.Items.Add("打开下载文件夹",null,(s,e)=>Dispatch(OpenFolder));menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("退出",null,(s,e)=>Dispatch(RequestQuit));
  var ico=Path.Combine(Entry.BaseDir,"Banarec.ico");tray=new Forms.NotifyIcon{Visible=true,Icon=File.Exists(ico)?new Drawing.Icon(ico):Drawing.SystemIcons.Application,Text="Banarec · 香蕉录屏",ContextMenuStrip=menu};tray.DoubleClick+=(s,e)=>Dispatch(Show);
  Window.SourceInitialized+=(s,e)=>{handle=new WindowInteropHelper(Window).Handle;HwndSource.FromHwnd(handle).AddHook(Hook);OverlayNative.SetWindowDisplayAffinity(handle,0x11);RegisterKeys();};
  Window.Closing+=(s,e)=>{if(!disposed){e.Cancel=true;Window.Hide();}};
  timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};timer.Tick+=(s,e)=>Tick();timer.Start();
  SystemEvents.SessionSwitch+=SessionChanged;Application.Current.SessionEnding+=(s,e)=>{if(State!="idle"){e.Cancel=true;RequestQuit();}};
  showEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\Banarec.Show");showWait=ThreadPool.RegisterWaitForSingleObject(showEvent,(o,t)=>Dispatch(Show),null,-1,false);
  if(startup){Window.ShowActivated=false;Window.Opacity=0;Window.Show();Window.Hide();Window.Opacity=1;Window.ShowActivated=true;}else Window.Show();
 }
 void Bind(string n,Action a){UI<Button>(n).Click+=(s,e)=>a();}
 void Dispatch(Action a){if(!disposed&&!Window.Dispatcher.HasShutdownStarted)Window.Dispatcher.BeginInvoke(a);}
 public void Show(){if(disposed)return;Window.Show();Window.WindowState=WindowState.Normal;Window.Activate();}
 public void Mode(string value){UI<FrameworkElement>("RecordPane").Visibility=value=="record"?Visibility.Visible:Visibility.Collapsed;UI<FrameworkElement>("ShotPane").Visibility=value=="shot"?Visibility.Visible:Visibility.Collapsed;UI<FrameworkElement>("SettingsPane").Visibility=value=="settings"?Visibility.Visible:Visibility.Collapsed;UI<FrameworkElement>("ModeTabs").Visibility=value=="settings"?Visibility.Collapsed:Visibility.Visible;}
 void RefreshRegion(){UI<TextBlock>("RegionSize").Text=area.Width+" × "+area.Height;UI<TextBlock>("RegionHint").Text=(area==Capture.Normalize(screen.Bounds,screen.Bounds)?"整个屏幕":"已框选区域")+" · 原始像素";}
 void UpdateQuality(){UI<TextBlock>("QualityLabel").Text="原生画质 · "+(UI<ComboBox>("Framerate").SelectedIndex==1?"60":"30")+" FPS";}
 void SettingsChanged(){if(loading)return;UpdateQuality();SaveSettings();if(handle!=IntPtr.Zero)RegisterScreenshotKey();}
 void ChangeStartup(bool enabled){if(loading)return;try{Startup.Set(enabled);Status(enabled?"已设置登录后驻留托盘":"已关闭登录自动启动");}catch(Exception ex){loading=true;UI<CheckBox>("AutoStart").IsChecked=Startup.Enabled;loading=false;Error(ex.Message);}}
 void LoadSettings(){try{var v=File.ReadAllLines(settings);UI<CheckBox>("SystemAudio").IsChecked=v[0]=="1";UI<CheckBox>("Microphone").IsChecked=v[1]=="1";UI<ComboBox>("Framerate").SelectedIndex=v[2]=="60"?1:0;UI<CheckBox>("Compatibility").IsChecked=v[3]=="1";UI<CheckBox>("ScreenshotHotkey").IsChecked=v.Length<5||v[4]=="1";}catch{}}
 void SaveSettings(){try{Directory.CreateDirectory(Path.GetDirectoryName(settings));File.WriteAllLines(settings,new[]{IsOn("SystemAudio")?"1":"0",IsOn("Microphone")?"1":"0",UI<ComboBox>("Framerate").SelectedIndex==1?"60":"30",IsOn("Compatibility")?"1":"0",IsOn("ScreenshotHotkey")?"1":"0"});}catch(Exception ex){Entry.Log(ex);}}
 void RegisterKeys(){if(!realHotkeys)return;recordKey=Native.RegisterHotKey(handle,1,0x4006,0x78);RegisterScreenshotKey();if(!recordKey)Status("录制快捷键被占用，请使用按钮或托盘");}
 void RegisterScreenshotKey(){
  if(!realHotkeys)return;
  if(shotKey){Native.UnregisterHotKey(handle,2);shotKey=false;}
  if(shortcut!=null){shortcut.Dispose();shortcut=null;}
  if(!IsOn("ScreenshotHotkey"))return;
  shotKey=Native.RegisterHotKey(handle,2,0x4003,0x41);
  if(!shotKey){shortcut=new ScreenshotShortcut(()=>Dispatch(async()=>await Screenshot(false)));if(shortcut.Active)Status("截图快捷键已就绪 · Ctrl + Alt + A");else Status("截图快捷键暂不可用，请使用截图按钮");}
 }
 IntPtr Hook(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled){if(msg==0x312){handled=true;if(wp.ToInt32()==1)Dispatch(async()=>{if(State=="idle")await Begin();else if(State!="selecting"&&State!="screenshot")Stop();});if(wp.ToInt32()==2)Dispatch(async()=>await Screenshot(false));}return IntPtr.Zero;}
 void Busy(bool value){foreach(string n in new[]{"Screens","FullButton","RegionButton","SystemAudio","Microphone","Framerate","Compatibility","ScreenshotButton"})UI<UIElement>(n).IsEnabled=!value;UI<Button>("StartButton").Content=value?"停止并保存":"开始录制";}
 Forms.DialogResult Select(RegionPicker p){picker=p;try{return PickerOverride!=null?PickerOverride(p):p.ShowDialog();}finally{picker=null;}}
 async Task PickRegion(){if(State!="idle")return;State="selecting";Busy(true);Window.Hide();await Task.Delay(180);try{using(var p=new RegionPicker(screen)){if(Select(p)==Forms.DialogResult.OK){area=p.Selected;RefreshRegion();}}}catch(Exception ex){Error(ex.Message);}finally{State="idle";Busy(false);if(quit)Shutdown();else Show();}}
 string NewFile(string prefix,string extension){string dir=OutputOverride??Native.Downloads();Directory.CreateDirectory(dir);return Path.Combine(dir,prefix+DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff")+"_"+Guid.NewGuid().ToString("N").Substring(0,4)+extension);}
 public async Task Screenshot(bool fromWindow){if(State!="idle"){Status("请先结束当前操作，再截图");return;}State="screenshot";Busy(true);showAfterShot=fromWindow;Window.Hide();await Task.Delay(180);try{
  using(var p=new RegionPicker(Forms.SystemInformation.VirtualScreen,true)){if(Select(p)==Forms.DialogResult.OK){using(var image=p.GetSelectionImage()){Drawing.Bitmap annotated;if(AnnotationOverride!=null)annotated=AnnotationOverride(image);else{var editor=new AnnotationWindow(image,Window.Resources);annotated=editor.ShowDialog()==true?editor.Result:null;}if(annotated!=null)using(annotated){string file=NewFile("截图_",".png");annotated.Save(file,Drawing.Imaging.ImageFormat.Png);LastFile=file;bool copied=await CopyPng(file);Status(copied?"截图已保存，并复制到剪贴板":"截图已保存；剪贴板正被占用");tray.ShowBalloonTip(3500,"截图已保存",copied?"已复制，可直接粘贴。":"可在下载文件夹查看。",Forms.ToolTipIcon.Info);}else Status("已取消截图");}}else Status("已取消截图");}
 }catch(Exception ex){Error("截图失败："+ex.Message);}finally{State="idle";Busy(false);if(quit)Shutdown();else if(showAfterShot)Show();}}
 public static async Task<bool> CopyPng(string path){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri(path);image.EndInit();image.Freeze();for(int i=0;i<5;i++){try{Clipboard.SetImage(image);return true;}catch(ExternalException){}catch{break;}await Task.Delay(90);}return false;}
 public async Task Begin(){if(State!="idle")return;State="countdown";Busy(true);int token=++generation;SaveSettings();try{
  if(!Forms.Screen.AllScreens.Any(s=>s.DeviceName==screen.DeviceName&&s.Bounds==screen.Bounds))throw new Exception("显示器配置有变化，请重新打开 Banarec。");
  string path=NewFile("录屏_",".mp4");if(new DriveInfo(Path.GetPathRoot(path)).AvailableFreeSpace<512L*1024*1024)throw new Exception("磁盘剩余空间不足 512 MB。");
  Window.Hide();border=new RecordingBorder(area);border.SetRecording(false);border.Show();hud=new Hud(Window.Resources,area,IsOn("SystemAudio"),IsOn("Microphone"));hud.StopRequested+=Stop;hud.Show();
  countdown=new Countdown(area);countdown.Show();for(int n=3;n>0;n--){countdown.Number.Text=n.ToString();hud.Set("即将录制  "+n,false,true);await Task.Delay(1000);if(token!=generation)return;}countdown.Dispose();countdown=null;await Task.Delay(180);if(token!=generation)return;
  State="starting";hud.Set("正在准备…",false,false);capture=new Capture();capture.Started+=()=>Dispatch(()=>{if(State=="starting"){State="recording";elapsed.Restart();border.SetRecording(true);hud.Set("录制中  00:00:00",false,false);}});capture.Completed+=p=>Dispatch(()=>Finish(p,null));capture.Failed+=e=>Dispatch(()=>Finish(null,e));
  capture.Start(screen,area,IsOn("SystemAudio"),IsOn("Microphone"),UI<ComboBox>("Framerate").SelectedIndex==1?60:30,path,!IsOn("Compatibility"));
 }catch(Exception ex){Finish(null,ex.Message);}}
 public void Stop(){if(State=="idle"||State=="saving"||State=="selecting"||State=="screenshot")return;if(State=="countdown"){generation++;ClearOverlays();State="idle";Busy(false);Status("已取消录制");if(quit)Shutdown();else Show();return;}State="saving";UI<Button>("StartButton").IsEnabled=false;if(hud!=null)hud.Set("正在保存…",true,false);try{capture.Stop();}catch(Exception ex){Finish(null,ex.Message);}}
 void Finish(string path,string error){ClearOverlays();if(capture!=null){capture.Dispose();capture=null;}elapsed.Stop();State="idle";Busy(false);UI<Button>("StartButton").IsEnabled=true;tray.Text="Banarec · 香蕉录屏";if(error!=null){Status("录制未完成");Error(error+"\n可在设置中开启兼容模式重试。");}else{LastFile=path;Status("录像已保存到下载文件夹");tray.ShowBalloonTip(3500,"录像已保存",Path.GetFileName(path),Forms.ToolTipIcon.Info);}if(quit)Shutdown();else Show();}
 void ClearOverlays(){if(countdown!=null){countdown.Dispose();countdown=null;}if(hud!=null){hud.Dispose();hud=null;}if(border!=null){border.Dispose();border=null;}}
 void Tick(){if(State!="recording")return;string t=elapsed.Elapsed.ToString(@"hh\:mm\:ss");if(hud!=null)hud.Set("录制中  "+t,false,false);tray.Text="Banarec · "+t;Status("正在录制  "+t);if(elapsed.Elapsed.TotalSeconds%5<0.3){try{if(new DriveInfo(Path.GetPathRoot(OutputOverride??Native.Downloads())).AvailableFreeSpace<256L*1024*1024)Stop();}catch{}}}
 void Error(string text){Status(text.Split('\n')[0]);if(realHotkeys)MessageBox.Show(text,"Banarec · 香蕉录屏",MessageBoxButton.OK,MessageBoxImage.Information);else throw new InvalidOperationException(text);}
 public void OpenFolder(){try{if(LastFile!=null&&File.Exists(LastFile))Process.Start("explorer.exe","/select,\""+LastFile+"\"");else Process.Start("explorer.exe",Native.Downloads());}catch(Exception ex){Error(ex.Message);}}
 void SessionChanged(object s,SessionSwitchEventArgs e){if(e.Reason==SessionSwitchReason.SessionLock)Dispatch(()=>{if(picker!=null)picker.Close();else Stop();});}
 public void RequestQuit(){quit=true;if(State=="idle")Shutdown();else if(picker!=null)picker.Close();else Stop();}
 void Shutdown(){if(disposed)return;disposed=true;SaveSettings();ClearOverlays();timer.Stop();if(recordKey)Native.UnregisterHotKey(handle,1);if(shotKey)Native.UnregisterHotKey(handle,2);if(shortcut!=null)shortcut.Dispose();showWait.Unregister(null);showEvent.Dispose();SystemEvents.SessionSwitch-=SessionChanged;tray.Visible=false;tray.Dispose();Window.Close();Application.Current.Shutdown();}
}

sealed class Hud:Window,IDisposable {
 readonly TextBlock title;readonly Button stop;bool dispose;
 public event Action StopRequested;
 public Hud(ResourceDictionary resources,Drawing.Rectangle area,bool system,bool mic){Resources=resources;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;ShowActivated=false;Width=376;Height=82;Title="Banarec · 录制中";
  var shell=new Border{Background=new SolidColorBrush(Color.FromRgb(250,250,251)),CornerRadius=new CornerRadius(17),BorderBrush=new SolidColorBrush(Color.FromRgb(224,224,228)),BorderThickness=new Thickness(1),Padding=new Thickness(17,12,12,12)};
  var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(106)});var info=new StackPanel{VerticalAlignment=VerticalAlignment.Center};title=new TextBlock{Text="准备录制",FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI"),FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(66,65,62))};info.Children.Add(title);info.Children.Add(new TextBlock{Text=(system?"系统声 开":"系统声 关")+"   ·   "+(mic?"麦克风 开":"麦克风 关"),Foreground=Brushes.Gray,FontSize=11,Margin=new Thickness(0,6,0,0)});
  info.Cursor=Cursors.SizeAll;info.MouseLeftButtonDown+=(s,e)=>{try{DragMove();}catch{}};grid.Children.Add(info);stop=new Button{Content="停止并保存",Background=new SolidColorBrush(Color.FromRgb(249,228,227)),Foreground=new SolidColorBrush(Color.FromRgb(179,67,61)),Padding=new Thickness(8,12,8,12),VerticalAlignment=VerticalAlignment.Center};stop.Click+=(s,e)=>{if(StopRequested!=null)StopRequested();};Grid.SetColumn(stop,1);grid.Children.Add(stop);shell.Child=grid;Content=shell;
  SourceInitialized+=(s,e)=>OverlayNative.SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle,0x11);
  Loaded+=(s,e)=>{var source=PresentationSource.FromVisual(this);var m=source.CompositionTarget.TransformFromDevice;var point=m.Transform(new Point(area.Left+area.Width/2,area.Top));var work=Forms.Screen.FromRectangle(area).WorkingArea;var tl=m.Transform(new Point(work.Left,work.Top));var br=m.Transform(new Point(work.Right,work.Bottom));Left=Math.Max(tl.X,Math.Min(point.X-Width/2,br.X-Width));Top=point.Y-Height-10;if(Top<tl.Y)Top=tl.Y+12;};
  Closing+=(s,e)=>{if(!dispose){e.Cancel=true;if(StopRequested!=null)StopRequested();}};
 }
 public void Set(string value,bool saving,bool counting){title.Text=value;stop.IsEnabled=!saving;stop.Content=saving?"正在保存…":counting?"取消":"停止并保存";}
 public void Dispose(){dispose=true;Close();}
}
