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

[assembly:AssemblyTitle("BanaStudio")]
[assembly:AssemblyProduct("BanaStudio")]
[assembly:AssemblyVersion("3.0.0.0")]
[assembly:AssemblyFileVersion("3.0.0.0")]

static class Entry {
 [STAThread] public static void Main(string[] args) {
  try {Native.SetProcessDpiAwarenessContext(new IntPtr(-4));}catch{}
  bool owned;using(var mutex=new Mutex(true,"Local\\Banarec.Desktop",out owned)) {
   if(!owned){if(!args.Contains("--startup"))using(var ev=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\Banarec.Show"))ev.Set();return;}
   var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
   app.DispatcherUnhandledException+=(s,e)=>{Log(e.Exception);MessageBox.Show("发生错误："+e.Exception.Message,"BanaStudio");e.Handled=true;};
   try {var controller=new Controller(args.Contains("--startup"),true);app.Run();GC.KeepAlive(controller);}catch(Exception ex){Log(ex);MessageBox.Show("启动失败："+ex.Message,"BanaStudio");}
  }
 }
 public static string BaseDir {get{return AppDomain.CurrentDomain.BaseDirectory;}}
 public static void Log(Exception ex){try{VoiceData.EnsureMigrated();string d=VoiceData.Root;Directory.CreateDirectory(d);File.AppendAllText(Path.Combine(d,"errors.log"),DateTime.Now+" "+ex+Environment.NewLine);}catch{}}
 public static Window LoadWindow(string resource){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))return (Window)XamlReader.Load(s);}
}

static class Startup {
 const string Key="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
 public static bool Enabled {get{using(var k=Registry.CurrentUser.OpenSubKey(Key))return k!=null&&k.GetValue("Banarec")!=null;}}
 public static void Set(bool enabled){using(var k=Registry.CurrentUser.CreateSubKey(Key)){if(enabled)k.SetValue("Banarec","\""+Path.Combine(Entry.BaseDir,"BanaStudio.exe")+"\" --startup");else k.DeleteValue("Banarec",false);k.DeleteValue("BanaStudio",false);}}
}

sealed class Controller {
 public readonly Window Window;
 readonly bool realHotkeys;bool loading=true,quit,disposed,recordKey,shotKey,showAfterShot;
 readonly Forms.NotifyIcon tray;readonly DispatcherTimer timer;readonly EventWaitHandle showEvent;readonly RegisteredWaitHandle showWait;
 readonly Forms.ToolStripMenuItem shotMenu,recordMenu;GlobalShortcutHook shortcut;
 HotkeyGesture screenshotGesture=HotkeyGesture.DefaultScreenshot,recordGesture=HotkeyGesture.DefaultRecording;VoiceOptions voiceOptions;System.Collections.Generic.List<VoiceTerm> voiceTerms;VoiceSession voiceSession;VoiceHud voiceHud;readonly Stopwatch voiceElapsed=new Stopwatch();bool voiceExternal;
 IntPtr handle;Capture capture;RecordingBorder border;Hud hud;Countdown countdown;RegionPicker picker;
 Forms.Screen screen;Drawing.Rectangle area;int generation;readonly Stopwatch elapsed=new Stopwatch();
 public string State {get;private set;} public string LastFile {get;private set;}
 public Func<Drawing.Bitmap,Drawing.Bitmap> AnnotationOverride=null;
 public string OutputOverride=null;public Func<RegionPicker,Forms.DialogResult> PickerOverride=null;
 readonly string settings=Path.Combine(VoiceData.Root,"settings.ini");
 public T UI<T>(string name) where T:class{return Window.FindName(name) as T;}
 bool IsOn(string n){return UI<CheckBox>(n).IsChecked==true;}
 void Status(string text){UI<TextBlock>("Status").Text=text;}
 public Controller(bool startup,bool enableHotkeys) {
  realHotkeys=enableHotkeys;VoiceData.EnsureMigrated();State="idle";Window=Entry.LoadWindow("Main.xaml");
  var iconPath=Path.Combine(Entry.BaseDir,"BanaStudio.png");if(File.Exists(iconPath)){var img=new BitmapImage(new Uri(iconPath));UI<Image>("BrandIcon").Source=img;Window.Icon=img;}
  UI<Grid>("Header").MouseLeftButtonDown+=(s,e)=>{if(e.OriginalSource is TextBlock||e.OriginalSource is Grid||e.OriginalSource is Image){try{Window.DragMove();}catch{}}};
  Bind("MinButton",()=>Window.WindowState=WindowState.Minimized);Bind("CloseButton",()=>Window.Hide());
  Bind("SettingsButton",()=>Mode("settings"));Bind("BackButton",()=>Mode(UI<RadioButton>("VoiceTab").IsChecked==true?"voice":UI<RadioButton>("ScreenshotTab").IsChecked==true?"shot":"record"));
  UI<RadioButton>("RecordTab").Checked+=(s,e)=>Mode("record");UI<RadioButton>("ScreenshotTab").Checked+=(s,e)=>Mode("shot");UI<RadioButton>("VoiceTab").Checked+=(s,e)=>Mode("voice");
  Bind("FullButton",()=>{area=Capture.Normalize(screen.Bounds,screen.Bounds);RefreshRegion();});Bind("RegionButton",async()=>await PickRegion());
  Bind("StartButton",async()=>{if(State=="idle")await Begin();else Stop();});Bind("ScreenshotButton",async()=>await Screenshot(true));Bind("FolderButton",OpenFolder);
  var screens=UI<ComboBox>("Screens");var all=Forms.Screen.AllScreens;for(int i=0;i<all.Length;i++)screens.Items.Add("显示器 "+(i+1)+(all[i].Primary?" · 主屏":"")+"  "+all[i].Bounds.Width+" × "+all[i].Bounds.Height);
  screens.SelectionChanged+=(s,e)=>{if(screens.SelectedIndex<0)return;screen=all[screens.SelectedIndex];area=Capture.Normalize(screen.Bounds,screen.Bounds);RefreshRegion();};screens.SelectedIndex=Array.FindIndex(all,s=>s.Primary);
  UI<ComboBox>("Framerate").SelectedIndex=0;LoadSettings();UI<CheckBox>("AutoStart").IsChecked=Startup.Enabled;
  foreach(string name in new[]{"SystemAudio","Microphone","Compatibility"}){var box=UI<CheckBox>(name);box.Checked+=(s,e)=>SettingsChanged();box.Unchecked+=(s,e)=>SettingsChanged();}
  UI<ComboBox>("Framerate").SelectionChanged+=(s,e)=>SettingsChanged();
  UI<CheckBox>("AutoStart").Checked+=(s,e)=>ChangeStartup(true);UI<CheckBox>("AutoStart").Unchecked+=(s,e)=>ChangeStartup(false);
  Bind("ScreenshotShortcutButton",()=>ChangeHotkey(true));Bind("RecordingShortcutButton",()=>ChangeHotkey(false));InitializeVoice();RefreshHotkeyLabels();
  loading=false;UpdateQuality();
  var menu=new Forms.ContextMenuStrip();menu.Items.Add("打开 BanaStudio",null,(s,e)=>Dispatch(Show));shotMenu=new Forms.ToolStripMenuItem("截图   "+screenshotGesture.Display,null,(s,e)=>Dispatch(async()=>await Screenshot(false)));menu.Items.Add(shotMenu);recordMenu=new Forms.ToolStripMenuItem("开始 / 停止录制   "+recordGesture.Display,null,(s,e)=>Dispatch(async()=>{if(State=="idle")await Begin();else Stop();}));menu.Items.Add(recordMenu);menu.Items.Add("打开下载文件夹",null,(s,e)=>Dispatch(OpenFolder));menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("退出",null,(s,e)=>Dispatch(RequestQuit));
  var ico=Path.Combine(Entry.BaseDir,"BanaStudio.ico");tray=new Forms.NotifyIcon{Visible=true,Icon=File.Exists(ico)?new Drawing.Icon(ico):Drawing.SystemIcons.Application,Text="BanaStudio",ContextMenuStrip=menu};tray.DoubleClick+=(s,e)=>Dispatch(Show);
  Window.SourceInitialized+=(s,e)=>{handle=new WindowInteropHelper(Window).Handle;HwndSource.FromHwnd(handle).AddHook(Hook);WindowBackdrop.Enable(Window);OverlayNative.SetWindowDisplayAffinity(handle,0x11);RegisterKeys();};
  Window.Closing+=(s,e)=>{if(!disposed){e.Cancel=true;Window.Hide();}};
  timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};timer.Tick+=(s,e)=>Tick();timer.Start();
  SystemEvents.SessionSwitch+=SessionChanged;Application.Current.SessionEnding+=(s,e)=>{if(State!="idle"||VoiceBusy){e.Cancel=true;RequestQuit();}};
  showEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\Banarec.Show");showWait=ThreadPool.RegisterWaitForSingleObject(showEvent,(o,t)=>Dispatch(Show),null,-1,false);
  if(startup){Window.ShowActivated=false;Window.Opacity=0;Window.Show();Window.Hide();Window.Opacity=1;Window.ShowActivated=true;}else Window.Show();
 }
 void SaveVoiceSettingsSafe(){if(VoiceBusy){Status("请先结束当前语音识别");return;}SaveVoiceSettings();}
 void ImportVoiceCredentialsSafe(){if(VoiceBusy){Status("请先结束当前语音识别");return;}ImportVoiceCredentials();}
 void InitializeVoice(){
  voiceOptions=VoiceData.LoadOptions();voiceTerms=VoiceData.LoadTerms();
  UI<ComboBox>("VoiceBackend").SelectedIndex=voiceOptions.Backend=="stream"?0:1;UI<ComboBox>("VoiceBehavior").SelectedIndex=voiceOptions.HoldMode?1:0;
  UI<PasswordBox>("VoiceApiKey").Password=voiceOptions.ApiKey;UI<TextBox>("VoiceAppKey").Text=voiceOptions.AppKey;UI<PasswordBox>("VoiceAccessKey").Password=voiceOptions.AccessKey;UI<TextBox>("VoiceResourceId").Text=voiceOptions.ResourceId;RefreshVoiceUi();
  Bind("VoiceStartButton",()=>Dispatch(async()=>await ToggleVoice(false)));Bind("VoiceCancelButton",()=>CancelVoice());Bind("VoiceLibraryButton",OpenVoiceLibrary);Bind("SaveVoiceSettingsButton",SaveVoiceSettingsSafe);Bind("ImportAsrConfigButton",ImportVoiceCredentialsSafe);Bind("VoiceShortcutButton",ChangeVoiceHotkey);Bind("VoiceAddTermButton",AddVoiceTerm);Bind("VoiceImportTermsButton",ImportVoiceTerms);Bind("VoiceExportTermsButton",ExportVoiceTerms);UI<ComboBox>("VoiceBackend").SelectionChanged+=(s,e)=>VoiceModeChanged();UI<ComboBox>("VoiceBehavior").SelectionChanged+=(s,e)=>VoiceModeChanged();
 }
 void VoiceModeChanged(){if(loading)return;if(voiceSession!=null&&voiceSession.State!=VoiceState.Idle){loading=true;UI<ComboBox>("VoiceBackend").SelectedIndex=voiceOptions.Backend=="stream"?0:1;UI<ComboBox>("VoiceBehavior").SelectedIndex=voiceOptions.HoldMode?1:0;loading=false;Status("请先结束当前语音识别");return;}voiceOptions.Backend=UI<ComboBox>("VoiceBackend").SelectedIndex==0?"stream":"flash";voiceOptions.HoldMode=UI<ComboBox>("VoiceBehavior").SelectedIndex==1;if(voiceOptions.Backend=="flash"){voiceOptions.Endpoint=VoiceFlashCredentials.DefaultEndpoint;voiceOptions.ResourceId=VoiceFlashCredentials.DefaultResourceId;}else{voiceOptions.Endpoint="wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async";if(voiceOptions.ResourceId==VoiceFlashCredentials.DefaultResourceId)voiceOptions.ResourceId="volc.seedasr.sauc.duration";}VoiceData.SaveOptions(voiceOptions);UI<TextBox>("VoiceResourceId").Text=voiceOptions.ResourceId;RefreshVoiceUi();RegisterKeys();}
 void RefreshVoiceUi(){UI<Button>("VoiceShortcutButton").Content=voiceOptions.Gesture.Display;int enabled=voiceTerms.Count(x=>x.enabled);UI<TextBlock>("VoiceTermCount").Text=enabled+" 个启用 · 前约 100 tokens 生效";UI<TextBlock>("VoiceServiceStatus").Text=voiceOptions.Ready?(voiceOptions.Backend=="stream"?"实时流式 · 待验证授权":"极速识别 · 已配置"):"尚未配置";}
 void SaveVoiceSettings(){voiceOptions.Backend=UI<ComboBox>("VoiceBackend").SelectedIndex==0?"stream":"flash";voiceOptions.HoldMode=UI<ComboBox>("VoiceBehavior").SelectedIndex==1;voiceOptions.ApiKey=UI<PasswordBox>("VoiceApiKey").Password.Trim();voiceOptions.AppKey=UI<TextBox>("VoiceAppKey").Text.Trim();voiceOptions.AccessKey=UI<PasswordBox>("VoiceAccessKey").Password;voiceOptions.ResourceId=UI<TextBox>("VoiceResourceId").Text.Trim();if(voiceOptions.Backend=="flash"){voiceOptions.Endpoint=VoiceFlashCredentials.DefaultEndpoint;voiceOptions.ResourceId=VoiceFlashCredentials.DefaultResourceId;}else if(!voiceOptions.Endpoint.StartsWith("wss",StringComparison.OrdinalIgnoreCase)){voiceOptions.Endpoint="wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async";if(voiceOptions.ResourceId==VoiceFlashCredentials.DefaultResourceId)voiceOptions.ResourceId="volc.seedasr.sauc.duration";}VoiceData.SaveOptions(voiceOptions);RegisterKeys();RefreshVoiceUi();Status("语音服务设置已保存");}
 void ImportVoiceCredentials(){try{voiceOptions=VoiceFlashCredentials.ImportAndSaveDefault();voiceOptions.Backend="flash";VoiceData.SaveOptions(voiceOptions);UI<ComboBox>("VoiceBackend").SelectedIndex=1;UI<PasswordBox>("VoiceApiKey").Password="";UI<TextBox>("VoiceAppKey").Text=voiceOptions.AppKey;UI<PasswordBox>("VoiceAccessKey").Password=voiceOptions.AccessKey;UI<TextBox>("VoiceResourceId").Text=voiceOptions.ResourceId;RefreshVoiceUi();Status("已安全导入本机 ASR 配置");}catch(Exception ex){Error(ex.Message);}}
 void ChangeVoiceHotkey(){if(voiceSession!=null&&voiceSession.State!=VoiceState.Idle){Status("请先结束语音识别");return;}UnregisterKeys();try{var d=new HotkeyCaptureWindow("语音识别",voiceOptions.Gesture,Window.Resources){Owner=Window};if(d.ShowDialog()!=true||d.Gesture==null)return;if(d.Gesture==screenshotGesture||d.Gesture==recordGesture){Error("这个组合键已用于截图或录屏，请换一个。");return;}voiceOptions.Modifiers=d.Gesture.Modifiers;voiceOptions.VirtualKey=d.Gesture.VirtualKey;VoiceData.SaveOptions(voiceOptions);RefreshVoiceUi();}finally{RegisterKeys();}}
 bool VoiceBusy {get{return voiceSession!=null&&voiceSession.State!=VoiceState.Idle;}}
 async Task ToggleVoice(bool externalTarget){if(State!="idle"){Status("请先结束当前录屏或截图操作");return;}if(voiceSession==null||voiceSession.State==VoiceState.Idle){if(voiceSession!=null){var old=voiceSession;voiceSession=null;old.Dispose();}voiceExternal=externalTarget;voiceElapsed.Restart();if(externalTarget){voiceHud=new VoiceHud(Window.Resources);voiceHud.CancelRequested+=CancelVoice;voiceHud.Show();}var session=new VoiceSession(voiceOptions,voiceTerms);voiceSession=session;session.Changed+=(s,t,l)=>Dispatch(()=>{if(object.ReferenceEquals(voiceSession,session)){VoiceChanged(s,t,l);if(s==VoiceState.Idle&&!string.IsNullOrEmpty(session.DeliveryStatus))Status(session.DeliveryStatus);}});try{await session.Start(externalTarget);}catch(Exception ex){if(object.ReferenceEquals(voiceSession,session)){voiceSession=null;session.Dispose();}if(voiceHud!=null){voiceHud.Dispose();voiceHud=null;}Error(ex.Message);}}else await voiceSession.Stop();}
 void SetVoiceConfigEnabled(bool enabled){foreach(string name in new[]{"VoiceBackend","VoiceBehavior","VoiceShortcutButton","ImportAsrConfigButton","SaveVoiceSettingsButton","VoiceApiKey","VoiceAppKey","VoiceAccessKey","VoiceResourceId","VoiceLibraryButton","VoiceAddTermButton","VoiceImportTermsButton","VoiceExportTermsButton"}){var item=UI<UIElement>(name);if(item!=null)item.IsEnabled=enabled;}}
 void VoiceChanged(VoiceState state,string text,double level){UI<ProgressBar>("VoiceLevel").Value=Math.Max(0,Math.Min(1,level));UI<TextBlock>("VoiceTranscript").Text=string.IsNullOrEmpty(text)?(state==VoiceState.Listening?"正在聆听…":state==VoiceState.Finalizing?"正在完成识别…":"按快捷键开始讲话"):text;UI<Button>("VoiceStartButton").Content=state==VoiceState.Listening?"结束识别":state==VoiceState.Finalizing?"正在完成…":"开始试录";UI<Button>("VoiceStartButton").IsEnabled=state!=VoiceState.Finalizing&&state!=VoiceState.Delivering;SetVoiceConfigEnabled(state==VoiceState.Idle||state==VoiceState.Error);if(voiceHud!=null){voiceHud.Update(state,text,level,voiceElapsed.Elapsed);if(state==VoiceState.Idle||state==VoiceState.Error){voiceHud.Dispose();voiceHud=null;voiceElapsed.Stop();}}if(state==VoiceState.Error)Status(text);}
 void CancelVoice(){var session=voiceSession;voiceSession=null;if(session!=null){session.Cancel();session.Dispose();}VoiceChanged(VoiceState.Idle,"",0);Status("已取消语音识别");}
 void AddVoiceTerm(){string word=UI<TextBox>("VoiceTermInput").Text.Trim();if(word.Length==0)return;try{voiceTerms=VoiceData.Merge(voiceTerms,new[]{VoiceData.NewTerm(word,new System.Collections.Generic.List<string>(),"manual")});VoiceData.SaveTerms(voiceTerms);UI<TextBox>("VoiceTermInput").Clear();RefreshVoiceUi();}catch(Exception ex){Error(ex.Message);}}
 void ImportVoiceTerms(){var d=new OpenFileDialog{Filter="词库文件|*.json;*.csv;*.txt|所有文件|*.*"};if(d.ShowDialog()!=true)return;try{voiceTerms=VoiceData.Merge(voiceTerms,VoiceData.Import(d.FileName));VoiceData.SaveTerms(voiceTerms);RefreshVoiceUi();Status("词库已合并导入");}catch(Exception ex){Error(ex.Message);}}
 void ExportVoiceTerms(){var d=new SaveFileDialog{Filter="JSON 完整备份|*.json|CSV 词表|*.csv",FileName="BanaStudio-词库.json"};if(d.ShowDialog()!=true)return;try{if(Path.GetExtension(d.FileName).Equals(".csv",StringComparison.OrdinalIgnoreCase))VoiceData.ExportCsv(d.FileName,voiceTerms);else VoiceData.ExportJson(d.FileName,voiceTerms);Status("词库已导出");}catch(Exception ex){Error(ex.Message);}}
 void OpenVoiceLibrary(){var dialog=new VoiceLibraryWindow(Window.Resources,voiceTerms,VoiceData.LoadHistory()){Owner=Window};dialog.TermsChanged+=items=>{voiceTerms=items;RefreshVoiceUi();};dialog.ImportRequested+=()=>{var d=new OpenFileDialog{Filter="词库文件|*.json;*.csv;*.txt|所有文件|*.*"};if(d.ShowDialog()==true)try{dialog.MergeImported(VoiceData.Import(d.FileName));}catch(Exception ex){Error(ex.Message);}};dialog.ExportRequested+=ExportVoiceTerms;dialog.ShowDialog();voiceTerms=dialog.Terms.ToList();RefreshVoiceUi();}
 void Bind(string n,Action a){UI<Button>(n).Click+=(s,e)=>a();}
 void Dispatch(Action a){if(!disposed&&!Window.Dispatcher.HasShutdownStarted)Window.Dispatcher.BeginInvoke(a);}
 public void Show(){if(disposed)return;Window.Show();Window.WindowState=WindowState.Normal;Window.Activate();}
 public void Mode(string value){UI<FrameworkElement>("RecordPane").Visibility=value=="record"?Visibility.Visible:Visibility.Collapsed;UI<FrameworkElement>("ShotPane").Visibility=value=="shot"?Visibility.Visible:Visibility.Collapsed;UI<FrameworkElement>("VoicePane").Visibility=value=="voice"?Visibility.Visible:Visibility.Collapsed;UI<FrameworkElement>("SettingsPane").Visibility=value=="settings"?Visibility.Visible:Visibility.Collapsed;UI<FrameworkElement>("ModeTabs").Visibility=value=="settings"?Visibility.Collapsed:Visibility.Visible;}
 void RefreshRegion(){UI<TextBlock>("RegionSize").Text=area.Width+" × "+area.Height;UI<TextBlock>("RegionHint").Text=(area==Capture.Normalize(screen.Bounds,screen.Bounds)?"整个屏幕":"已框选区域")+" · 原始像素";}
 void UpdateQuality(){UI<TextBlock>("QualityLabel").Text="原生画质 · "+(UI<ComboBox>("Framerate").SelectedIndex==1?"60":"30")+" FPS";}
 void SettingsChanged(){if(loading)return;UpdateQuality();SaveSettings();}
 void ChangeStartup(bool enabled){if(loading)return;try{Startup.Set(enabled);Status(enabled?"已设置登录后驻留托盘":"已关闭登录自动启动");}catch(Exception ex){loading=true;UI<CheckBox>("AutoStart").IsChecked=Startup.Enabled;loading=false;Error(ex.Message);}}
 void LoadSettings(){try{var v=File.ReadAllLines(settings);UI<CheckBox>("SystemAudio").IsChecked=v[0]=="1";UI<CheckBox>("Microphone").IsChecked=v[1]=="1";UI<ComboBox>("Framerate").SelectedIndex=v[2]=="60"?1:0;UI<CheckBox>("Compatibility").IsChecked=v[3]=="1";if(v.Length>=9&&v[4]=="2"){screenshotGesture=ReadGesture(v[5],v[6],HotkeyGesture.DefaultScreenshot);recordGesture=ReadGesture(v[7],v[8],HotkeyGesture.DefaultRecording);}}catch{}}
 static HotkeyGesture ReadGesture(string modifiers,string key,HotkeyGesture fallback){uint m;int k;if(uint.TryParse(modifiers,out m)&&int.TryParse(key,out k)){var value=new HotkeyGesture(m,k);if(value.Valid)return value;}return fallback;}
 void SaveSettings(){try{Directory.CreateDirectory(Path.GetDirectoryName(settings));File.WriteAllLines(settings,new[]{IsOn("SystemAudio")?"1":"0",IsOn("Microphone")?"1":"0",UI<ComboBox>("Framerate").SelectedIndex==1?"60":"30",IsOn("Compatibility")?"1":"0","2",screenshotGesture.Modifiers.ToString(),screenshotGesture.VirtualKey.ToString(),recordGesture.Modifiers.ToString(),recordGesture.VirtualKey.ToString()});}catch(Exception ex){Entry.Log(ex);}}
 void RefreshHotkeyLabels(){UI<Button>("ScreenshotShortcutButton").Content=screenshotGesture.Display;UI<Button>("RecordingShortcutButton").Content=recordGesture.Display;UI<TextBlock>("ScreenshotHotkeyHero").Text=screenshotGesture.Display.Replace(" + ","  +  ");UI<TextBlock>("RecordHotkeyHint").Text="3 秒倒计时   ·   "+recordGesture.Display+" 开始 / 停止";if(shotMenu!=null)shotMenu.Text="截图   "+screenshotGesture.Display;if(recordMenu!=null)recordMenu.Text="开始 / 停止录制   "+recordGesture.Display;}
 void ChangeHotkey(bool screenshot){if(State!="idle"){Status("请先结束当前操作，再修改快捷键");return;}UnregisterKeys();try{var dialog=new HotkeyCaptureWindow(screenshot?"截图":"录屏",screenshot?screenshotGesture:recordGesture,Window.Resources){Owner=Window};if(dialog.ShowDialog()!=true||dialog.Gesture==null)return;if(screenshot&&dialog.Gesture==recordGesture||!screenshot&&dialog.Gesture==screenshotGesture||voiceOptions!=null&&dialog.Gesture==voiceOptions.Gesture){Error("这个组合键已用于另一个功能，请换一个。");return;}if(screenshot)screenshotGesture=dialog.Gesture;else recordGesture=dialog.Gesture;SaveSettings();RefreshHotkeyLabels();Status((screenshot?"截图":"录屏")+"快捷键已设为 "+dialog.Gesture.Display);}finally{RegisterKeys();}}
 void UnregisterKeys(){if(recordKey){Native.UnregisterHotKey(handle,1);recordKey=false;}if(shotKey){Native.UnregisterHotKey(handle,2);shotKey=false;}if(shortcut!=null){shortcut.Dispose();shortcut=null;}}
 void RegisterKeys(){
  if(!realHotkeys||handle==IntPtr.Zero)return;UnregisterKeys();var fallback=new System.Collections.Generic.List<GlobalShortcutHook.Binding>();recordKey=Native.RegisterHotKey(handle,1,recordGesture.Modifiers|0x4000,(uint)recordGesture.VirtualKey);shotKey=Native.RegisterHotKey(handle,2,screenshotGesture.Modifiers|0x4000,(uint)screenshotGesture.VirtualKey);
  if(!recordKey)fallback.Add(new GlobalShortcutHook.Binding(recordGesture,()=>Dispatch(async()=>{if(State=="idle")await Begin();else if(State!="selecting"&&State!="screenshot")Stop();})));if(!shotKey)fallback.Add(new GlobalShortcutHook.Binding(screenshotGesture,()=>Dispatch(async()=>await Screenshot(false))));fallback.Add(new GlobalShortcutHook.Binding(voiceOptions.Gesture,()=>Dispatch(async()=>await ToggleVoice(true)),()=>{if(voiceOptions.HoldMode)Dispatch(async()=>{if(voiceSession!=null)await voiceSession.Stop();});}));
  fallback.Add(new GlobalShortcutHook.Binding(new HotkeyGesture(0,0x1B),()=>Dispatch(()=>CancelVoice()),null,()=>voiceSession!=null&&voiceSession.State!=VoiceState.Idle));if(fallback.Count>0){shortcut=new GlobalShortcutHook(fallback);if(!shortcut.Active)Status("部分全局快捷键暂不可用，请使用窗口或托盘");else Status("快捷键已就绪 · 兼容模式");}
 }
 public void SetHotkeysForTest(HotkeyGesture screenshot,HotkeyGesture recording){screenshotGesture=screenshot;recordGesture=recording;SaveSettings();RefreshHotkeyLabels();RegisterKeys();}
 IntPtr Hook(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled){if(msg==0x312){handled=true;if(wp.ToInt32()==1)Dispatch(async()=>{if(State=="idle")await Begin();else if(State!="selecting"&&State!="screenshot")Stop();});if(wp.ToInt32()==2)Dispatch(async()=>await Screenshot(false));}return IntPtr.Zero;}
 void Busy(bool value){foreach(string n in new[]{"Screens","FullButton","RegionButton","SystemAudio","Microphone","Framerate","Compatibility","ScreenshotButton"})UI<UIElement>(n).IsEnabled=!value;UI<Button>("StartButton").Content=value?"停止并保存":"开始录制";}
 Forms.DialogResult Select(RegionPicker p){picker=p;try{return PickerOverride!=null?PickerOverride(p):p.ShowDialog();}finally{picker=null;}}
 async Task PickRegion(){if(State!="idle"||VoiceBusy){Status("请先结束当前操作");return;}State="selecting";Busy(true);Window.Hide();await Task.Delay(180);try{using(var p=new RegionPicker(screen)){if(Select(p)==Forms.DialogResult.OK){area=p.Selected;RefreshRegion();}}}catch(Exception ex){Error(ex.Message);}finally{State="idle";Busy(false);if(quit)Shutdown();else Show();}}
 string NewFile(string prefix,string extension){string dir=OutputOverride??Native.Downloads();Directory.CreateDirectory(dir);return Path.Combine(dir,prefix+DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff")+"_"+Guid.NewGuid().ToString("N").Substring(0,4)+extension);}
 public async Task Screenshot(bool fromWindow){if(State!="idle"||VoiceBusy){Status("请先结束当前操作，再截图");return;}State="screenshot";Busy(true);showAfterShot=fromWindow;Window.Hide();await Task.Delay(180);try{
  using(var p=new RegionPicker(Forms.SystemInformation.VirtualScreen,true)){if(Select(p)==Forms.DialogResult.OK){using(var image=p.GetSelectionImage()){Drawing.Bitmap annotated;if(AnnotationOverride!=null)annotated=AnnotationOverride(image);else{var editor=new AnnotationWindow(image,Window.Resources);annotated=editor.ShowDialog()==true?editor.Result:null;}if(annotated!=null)using(annotated){string file=NewFile("截图_",".png");annotated.Save(file,Drawing.Imaging.ImageFormat.Png);LastFile=file;bool copied=await CopyPng(file);Status(copied?"截图已保存，并复制到剪贴板":"截图已保存；剪贴板正被占用");tray.ShowBalloonTip(3500,"截图已保存",copied?"已复制，可直接粘贴。":"可在下载文件夹查看。",Forms.ToolTipIcon.Info);}else Status("已取消截图");}}else Status("已取消截图");}
 }catch(Exception ex){Error("截图失败："+ex.Message);}finally{State="idle";Busy(false);if(quit)Shutdown();else if(showAfterShot)Show();}}
 public static async Task<bool> CopyPng(string path){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri(path);image.EndInit();image.Freeze();for(int i=0;i<5;i++){try{Clipboard.SetImage(image);return true;}catch(ExternalException){}catch{break;}await Task.Delay(90);}return false;}
 public async Task Begin(){if(State!="idle"||VoiceBusy){Status("请先结束当前操作，再录屏");return;}State="countdown";Busy(true);int token=++generation;SaveSettings();try{
  if(!Forms.Screen.AllScreens.Any(s=>s.DeviceName==screen.DeviceName&&s.Bounds==screen.Bounds))throw new Exception("显示器配置有变化，请重新打开 BanaStudio。");
  string path=NewFile("录屏_",".mp4");if(new DriveInfo(Path.GetPathRoot(path)).AvailableFreeSpace<512L*1024*1024)throw new Exception("磁盘剩余空间不足 512 MB。");
  Window.Hide();border=new RecordingBorder(area);border.SetRecording(false);border.Show();hud=new Hud(Window.Resources,area,IsOn("SystemAudio"),IsOn("Microphone"));hud.StopRequested+=Stop;hud.Show();
  countdown=new Countdown(area);countdown.Show();for(int n=3;n>0;n--){countdown.Number.Text=n.ToString();hud.Set("即将录制  "+n,false,true);await Task.Delay(1000);if(token!=generation)return;}countdown.Dispose();countdown=null;await Task.Delay(180);if(token!=generation)return;
  State="starting";hud.Set("正在准备…",false,false);capture=new Capture();capture.Started+=()=>Dispatch(()=>{if(State=="starting"){State="recording";elapsed.Restart();border.SetRecording(true);hud.Set("录制中  00:00:00",false,false);}});capture.Completed+=p=>Dispatch(()=>Finish(p,null));capture.Failed+=e=>Dispatch(()=>Finish(null,e));
  capture.Start(screen,area,IsOn("SystemAudio"),IsOn("Microphone"),UI<ComboBox>("Framerate").SelectedIndex==1?60:30,path,!IsOn("Compatibility"));
 }catch(Exception ex){Finish(null,ex.Message);}}
 public void Stop(){if(State=="idle"||State=="saving"||State=="selecting"||State=="screenshot")return;if(State=="countdown"){generation++;ClearOverlays();State="idle";Busy(false);Status("已取消录制");if(quit)Shutdown();else Show();return;}State="saving";UI<Button>("StartButton").IsEnabled=false;if(hud!=null)hud.Set("正在保存…",true,false);try{capture.Stop();}catch(Exception ex){Finish(null,ex.Message);}}
 void Finish(string path,string error){ClearOverlays();if(capture!=null){capture.Dispose();capture=null;}elapsed.Stop();State="idle";Busy(false);UI<Button>("StartButton").IsEnabled=true;tray.Text="BanaStudio";if(error!=null){Status("录制未完成");Error(error+"\n可在设置中开启兼容模式重试。");}else{LastFile=path;Status("录像已保存到下载文件夹");tray.ShowBalloonTip(3500,"录像已保存",Path.GetFileName(path),Forms.ToolTipIcon.Info);}if(quit)Shutdown();else Show();}
 void ClearOverlays(){if(countdown!=null){countdown.Dispose();countdown=null;}if(hud!=null){hud.Dispose();hud=null;}if(border!=null){border.Dispose();border=null;}}
 void Tick(){if(State!="recording")return;string t=elapsed.Elapsed.ToString(@"hh\:mm\:ss");if(hud!=null)hud.Set("录制中  "+t,false,false);tray.Text="BanaStudio · "+t;Status("正在录制  "+t);if(elapsed.Elapsed.TotalSeconds%5<0.3){try{if(new DriveInfo(Path.GetPathRoot(OutputOverride??Native.Downloads())).AvailableFreeSpace<256L*1024*1024)Stop();}catch{}}}
 void Error(string text){Status(text.Split('\n')[0]);if(realHotkeys)MessageBox.Show(text,"BanaStudio",MessageBoxButton.OK,MessageBoxImage.Information);else throw new InvalidOperationException(text);}
 public void OpenFolder(){try{if(LastFile!=null&&File.Exists(LastFile))Process.Start("explorer.exe","/select,\""+LastFile+"\"");else Process.Start("explorer.exe",Native.Downloads());}catch(Exception ex){Error(ex.Message);}}
 void SessionChanged(object s,SessionSwitchEventArgs e){if(e.Reason==SessionSwitchReason.SessionLock)Dispatch(()=>{if(VoiceBusy)CancelVoice();if(picker!=null)picker.Close();else Stop();});}
 void DisposeVoice(){var session=voiceSession;voiceSession=null;if(session!=null){try{session.Cancel();}catch{}try{session.Dispose();}catch{}}if(voiceHud!=null){voiceHud.Dispose();voiceHud=null;}voiceElapsed.Stop();}
 public void RequestQuit(){quit=true;DisposeVoice();if(State=="idle")Shutdown();else if(picker!=null)picker.Close();else Stop();}
 void Shutdown(){if(disposed)return;disposed=true;DisposeVoice();SaveSettings();ClearOverlays();timer.Stop();if(recordKey)Native.UnregisterHotKey(handle,1);if(shotKey)Native.UnregisterHotKey(handle,2);if(shortcut!=null)shortcut.Dispose();showWait.Unregister(null);showEvent.Dispose();SystemEvents.SessionSwitch-=SessionChanged;tray.Visible=false;tray.Dispose();Window.Close();Application.Current.Shutdown();}
}

sealed class Hud:Window,IDisposable {
 readonly TextBlock title;readonly Button stop;bool dispose;
 public event Action StopRequested;
 public Hud(ResourceDictionary resources,Drawing.Rectangle area,bool system,bool mic){Resources=resources;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=false;Background=new SolidColorBrush(Color.FromArgb(1,255,255,255));Topmost=true;ShowInTaskbar=false;ShowActivated=false;Width=376;Height=82;Title="BanaStudio · 录制中";
  var shell=new Border{Background=new SolidColorBrush(Color.FromArgb(205,250,250,251)),CornerRadius=new CornerRadius(17),BorderBrush=new SolidColorBrush(Color.FromArgb(180,255,255,255)),BorderThickness=new Thickness(1),Padding=new Thickness(17,12,12,12)};
  var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(106)});var info=new StackPanel{VerticalAlignment=VerticalAlignment.Center};title=new TextBlock{Text="准备录制",FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI"),FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(66,65,62))};info.Children.Add(title);info.Children.Add(new TextBlock{Text=(system?"系统声 开":"系统声 关")+"   ·   "+(mic?"麦克风 开":"麦克风 关"),Foreground=Brushes.Gray,FontSize=11,Margin=new Thickness(0,6,0,0)});
  info.Cursor=Cursors.SizeAll;info.MouseLeftButtonDown+=(s,e)=>{try{DragMove();}catch{}};grid.Children.Add(info);stop=new Button{Content="停止并保存",Background=new SolidColorBrush(Color.FromRgb(249,228,227)),Foreground=new SolidColorBrush(Color.FromRgb(179,67,61)),Padding=new Thickness(8,12,8,12),VerticalAlignment=VerticalAlignment.Center};stop.Click+=(s,e)=>{if(StopRequested!=null)StopRequested();};Grid.SetColumn(stop,1);grid.Children.Add(stop);shell.Child=grid;Content=shell;
  SourceInitialized+=(s,e)=>{WindowBackdrop.Enable(this);OverlayNative.SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle,0x11);};
  Loaded+=(s,e)=>{var source=PresentationSource.FromVisual(this);var m=source.CompositionTarget.TransformFromDevice;var point=m.Transform(new Point(area.Left+area.Width/2,area.Top));var work=Forms.Screen.FromRectangle(area).WorkingArea;var tl=m.Transform(new Point(work.Left,work.Top));var br=m.Transform(new Point(work.Right,work.Bottom));Left=Math.Max(tl.X,Math.Min(point.X-Width/2,br.X-Width));Top=point.Y-Height-10;if(Top<tl.Y)Top=tl.Y+12;};
  Closing+=(s,e)=>{if(!dispose){e.Cancel=true;if(StopRequested!=null)StopRequested();}};
 }
 public void Set(string value,bool saving,bool counting){title.Text=value;stop.IsEnabled=!saving;stop.Content=saving?"正在保存…":counting?"取消":"停止并保存";}
 public void Dispose(){dispose=true;Close();}
}
