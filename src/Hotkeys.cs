using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

sealed class HotkeyGesture : IEquatable<HotkeyGesture> {
 public const uint Alt=0x1,Control=0x2,Shift=0x4;
 public readonly uint Modifiers;public readonly int VirtualKey;
 public static readonly HotkeyGesture DefaultScreenshot=new HotkeyGesture(Control|Alt,0x41);
 public static readonly HotkeyGesture DefaultRecording=new HotkeyGesture(Control|Shift,0x78);
 public static readonly HotkeyGesture DefaultVoice=new HotkeyGesture(Control|Alt,0x20);
 public HotkeyGesture(uint modifiers,int virtualKey){Modifiers=modifiers&(Alt|Control|Shift);VirtualKey=virtualKey;}
 public bool Valid {get{return Modifiers!=0&&VirtualKey>0&&!IsModifier(VirtualKey);}}
 public string Display {get{var values=new List<string>();if((Modifiers&Control)!=0)values.Add("Ctrl");if((Modifiers&Alt)!=0)values.Add("Alt");if((Modifiers&Shift)!=0)values.Add("Shift");values.Add(KeyName(VirtualKey));return string.Join(" + ",values);}}
 public bool Equals(HotkeyGesture other){return !ReferenceEquals(other,null)&&Modifiers==other.Modifiers&&VirtualKey==other.VirtualKey;}
 public override bool Equals(object value){return Equals(value as HotkeyGesture);}
 public override int GetHashCode(){return ((int)Modifiers*397)^VirtualKey;}
 public static bool operator ==(HotkeyGesture a,HotkeyGesture b){return ReferenceEquals(a,b)||(!ReferenceEquals(a,null)&&a.Equals(b));}
 public static bool operator !=(HotkeyGesture a,HotkeyGesture b){return !(a==b);}
 public static bool TryCreate(KeyEventArgs e,out HotkeyGesture gesture){
  gesture=null;Key key=e.Key==Key.System?e.SystemKey:e.Key;int vk=KeyInterop.VirtualKeyFromKey(key);if(IsModifier(vk)||vk==0x1B||vk==0x09)return false;
  uint modifiers=0;if((Keyboard.Modifiers&ModifierKeys.Control)!=0)modifiers|=Control;if((Keyboard.Modifiers&ModifierKeys.Alt)!=0)modifiers|=Alt;if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)modifiers|=Shift;
  gesture=new HotkeyGesture(modifiers,vk);return gesture.Valid;
 }
 static bool IsModifier(int vk){return vk==0x10||vk==0x11||vk==0x12||vk==0x5B||vk==0x5C||vk==0xA0||vk==0xA1||vk==0xA2||vk==0xA3||vk==0xA4||vk==0xA5;}
 static string KeyName(int vk){if(vk>=0x41&&vk<=0x5A)return ((char)vk).ToString();if(vk>=0x30&&vk<=0x39)return ((char)vk).ToString();if(vk>=0x70&&vk<=0x87)return "F"+(vk-0x6F);switch(vk){case 0x20:return "Space";case 0x2C:return "Print Screen";case 0x2D:return "Insert";case 0x2E:return "Delete";case 0x24:return "Home";case 0x23:return "End";case 0x21:return "Page Up";case 0x22:return "Page Down";case 0x25:return "←";case 0x26:return "↑";case 0x27:return "→";case 0x28:return "↓";}var key=KeyInterop.KeyFromVirtualKey(vk);return key==Key.None?"Key "+vk:key.ToString();}
}

sealed class GlobalShortcutHook : IDisposable {
 public sealed class Binding {public readonly HotkeyGesture Gesture;public readonly Action Trigger,Release;public readonly Func<bool> Enabled;internal bool Held;public Binding(HotkeyGesture gesture,Action trigger,Action release=null,Func<bool> enabled=null){Gesture=gesture;Trigger=trigger;Release=release;Enabled=enabled;}}
 delegate IntPtr Callback(int code,IntPtr message,IntPtr data);
 [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,Callback callback,IntPtr module,uint thread);
 [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
 [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
 readonly Callback callback;readonly List<Binding> bindings;IntPtr hook;
 public bool Active {get{return hook!=IntPtr.Zero;}}
 public GlobalShortcutHook(IEnumerable<Binding> bindings){this.bindings=new List<Binding>(bindings);callback=Handle;hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);}
 static bool Down(int key){return (GetAsyncKeyState(key)&0x8000)!=0;}
 static uint CurrentModifiers(){if(Down(0x5B)||Down(0x5C))return 0x80000000;uint value=0;if(Down(0x11))value|=HotkeyGesture.Control;if(Down(0x12))value|=HotkeyGesture.Alt;if(Down(0x10))value|=HotkeyGesture.Shift;return value;}
 IntPtr Handle(int code,IntPtr message,IntPtr data){
  if(code>=0){int vk=Marshal.ReadInt32(data),msg=message.ToInt32();bool down=msg==0x100||msg==0x104,up=msg==0x101||msg==0x105;
   foreach(var binding in bindings){if(vk!=binding.Gesture.VirtualKey)continue;if(up&&binding.Held){binding.Held=false;try{if(binding.Release!=null)binding.Release();}catch{}return new IntPtr(1);}if(down&&(binding.Held||CurrentModifiers()==binding.Gesture.Modifiers)){if(!binding.Held&&binding.Enabled!=null&&!binding.Enabled())continue;if(!binding.Held){binding.Held=true;try{binding.Trigger();}catch{}}return new IntPtr(1);}}
  }
  return CallNextHookEx(hook,code,message,data);
 }
 public void Dispose(){if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}}
}

sealed class HotkeyCaptureWindow : Window {
 readonly TextBlock preview;readonly TextBlock hint;public HotkeyGesture Gesture {get;private set;}
 public HotkeyCaptureWindow(string action,HotkeyGesture current,ResourceDictionary resources){
  Resources=resources;Title="设置快捷键";Width=420;Height=250;WindowStartupLocation=WindowStartupLocation.CenterOwner;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=false;Background=new SolidColorBrush(Color.FromArgb(1,255,255,255));ShowInTaskbar=false;
  var shell=new Border{Margin=new Thickness(0),CornerRadius=new CornerRadius(20),Background=Brush("#D9F8F8FA"),BorderBrush=Brush("#B8FFFFFF"),BorderThickness=new Thickness(1),Padding=new Thickness(28,24,28,24)};
  var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  var content=new StackPanel();content.Children.Add(new TextBlock{Text="设置"+action+"快捷键",FontSize=20,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center});content.Children.Add(new TextBlock{Text="请按下包含 Ctrl、Alt 或 Shift 的组合键",Foreground=Brush("#777980"),FontSize=12,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,10,0,18)});
  var keyBox=new Border{Background=Brush("#BFFFFFFF"),BorderBrush=Brush("#C8FFFFFF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(18,13,18,13),HorizontalAlignment=HorizontalAlignment.Stretch};preview=new TextBlock{Text=current.Display,FontSize=17,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center};keyBox.Child=preview;content.Children.Add(keyBox);
  hint=new TextBlock{Text="等待输入…",Foreground=Brush("#9A7A25"),FontSize=11,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,10,0,0)};content.Children.Add(hint);layout.Children.Add(content);
  var buttons=new Grid{Margin=new Thickness(0,18,0,0)};buttons.ColumnDefinitions.Add(new ColumnDefinition());buttons.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});buttons.ColumnDefinitions.Add(new ColumnDefinition());var cancel=new Button{Content="取消",Height=40};cancel.Click+=(s,e)=>DialogResult=false;buttons.Children.Add(cancel);var reset=new Button{Content="恢复默认",Height=40,Background=Brush("#E9BE46"),Foreground=Brush("#493B18")};reset.Click+=(s,e)=>{Gesture=action=="截图"?HotkeyGesture.DefaultScreenshot:action=="语音识别"?HotkeyGesture.DefaultVoice:HotkeyGesture.DefaultRecording;DialogResult=true;};Grid.SetColumn(reset,2);buttons.Children.Add(reset);Grid.SetRow(buttons,1);layout.Children.Add(buttons);shell.Child=layout;Content=shell;
  PreviewKeyDown+=Capture;SourceInitialized+=(s,e)=>WindowBackdrop.Enable(this);Loaded+=(s,e)=>Keyboard.Focus(this);
 }
 void Capture(object sender,KeyEventArgs e){e.Handled=true;if(e.Key==Key.Escape){DialogResult=false;return;}HotkeyGesture value;if(!HotkeyGesture.TryCreate(e,out value)){hint.Text="请同时按住 Ctrl、Alt 或 Shift";return;}Gesture=value;preview.Text=value.Display;hint.Text="已设置";DialogResult=true;}
 static Brush Brush(string value){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));}
}
