using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;

sealed class VoiceTarget {
 [StructLayout(LayoutKind.Sequential)] struct GuiInfo {public int cbSize;public uint flags;public IntPtr hwndActive,hwndFocus,hwndCapture,hwndMenuOwner,hwndMoveSize,hwndCaret;public Rect rcCaret;}
 [StructLayout(LayoutKind.Sequential)] struct Rect {public int l,t,r,b;}
 [StructLayout(LayoutKind.Explicit,Size=40)] struct Input {[FieldOffset(0)]public uint type;[FieldOffset(8)]public ushort key;[FieldOffset(12)]public uint flags;}
 sealed class Snapshot {public IntPtr Foreground,Focus;public int[] RuntimeId;public bool Password;}
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,IntPtr p);
 [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint id,ref GuiInfo info);
 [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int k);
 [DllImport("user32.dll")] static extern uint SendInput(uint n,Input[] p,int size);
 readonly Snapshot captured;
 VoiceTarget(Snapshot value){captured=value;}

 public static VoiceTarget Capture(){Snapshot value;return TrySnapshot(out value)?new VoiceTarget(value):null;}
 public bool StillValid(){
  if(captured==null||captured.Password||captured.Foreground==IntPtr.Zero||captured.Focus==IntPtr.Zero||!IsWindow(captured.Foreground)||!IsWindow(captured.Focus))return false;
  Snapshot current;if(!TrySnapshot(out current))return false;
  return current.Foreground==captured.Foreground&&current.Focus==captured.Focus&&!current.Password&&current.Password==captured.Password&&current.RuntimeId.SequenceEqual(captured.RuntimeId);
 }
 public async Task<bool> Paste(string text,CancellationToken cancellationToken=default(CancellationToken)){
  cancellationToken.ThrowIfCancellationRequested();
  if(string.IsNullOrEmpty(text)||Thread.CurrentThread.GetApartmentState()!=ApartmentState.STA||SynchronizationContext.Current==null||!StillValid())return false;
  bool copied=false;
  for(int i=0;i<6&&!copied;i++){
   cancellationToken.ThrowIfCancellationRequested();
   bool retry=false;try{Clipboard.SetText(text);copied=true;}catch{retry=true;}
   if(retry&&i<5)await Task.Delay(80,cancellationToken);
   cancellationToken.ThrowIfCancellationRequested();
   if(Thread.CurrentThread.GetApartmentState()!=ApartmentState.STA)return false;
  }
  if(!copied)return false;
  for(int i=0;i<20&&ModifiersDown();i++){await Task.Delay(25,cancellationToken);cancellationToken.ThrowIfCancellationRequested();if(Thread.CurrentThread.GetApartmentState()!=ApartmentState.STA)return false;}
  cancellationToken.ThrowIfCancellationRequested();
  if(ModifiersDown()||!StillValid())return false;
  cancellationToken.ThrowIfCancellationRequested();
  var keys=new[]{Key(0x11,false),Key(0x56,false),Key(0x56,true),Key(0x11,true)};
  return SendInput((uint)keys.Length,keys,Marshal.SizeOf(typeof(Input)))==keys.Length;
 }
 static bool TrySnapshot(out Snapshot value){
  value=null;
  try{
   IntPtr foreground=GetForegroundWindow();if(foreground==IntPtr.Zero)return false;
   var info=new GuiInfo{cbSize=Marshal.SizeOf(typeof(GuiInfo))};
   if(!GetGUIThreadInfo(GetWindowThreadProcessId(foreground,IntPtr.Zero),ref info)||info.hwndFocus==IntPtr.Zero)return false;
   var element=AutomationElement.FocusedElement;if(element==null)return false;
   int[] runtime=element.GetRuntimeId();if(runtime==null||runtime.Length==0)return false;
   object password=element.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty,true);if(!(password is bool))return false;
   object enabled=element.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty,true);if(!(enabled is bool)||!(bool)enabled)return false;
   object focusable=element.GetCurrentPropertyValue(AutomationElement.IsKeyboardFocusableProperty,true);if(!(focusable is bool)||!(bool)focusable)return false;
   object typeValue=element.GetCurrentPropertyValue(AutomationElement.ControlTypeProperty,true);var type=typeValue as ControlType;
   if(type!=ControlType.Edit&&type!=ControlType.Document)return false;
   object pattern;
   if(element.TryGetCurrentPattern(ValuePattern.Pattern,out pattern)){
    var valuePattern=pattern as ValuePattern;if(valuePattern==null||valuePattern.Current.IsReadOnly)return false;
   }else if(!element.TryGetCurrentPattern(TextPattern.Pattern,out pattern)||!(pattern is TextPattern))return false;
   value=new Snapshot{Foreground=foreground,Focus=info.hwndFocus,RuntimeId=(int[])runtime.Clone(),Password=(bool)password};return true;
  }catch{return false;}
 }
 static bool ModifiersDown(){return GetAsyncKeyState(0x10)<0||GetAsyncKeyState(0x11)<0||GetAsyncKeyState(0x12)<0;}
 static Input Key(ushort key,bool up){return new Input{type=1,key=key,flags=up?2u:0u};}
}

sealed class WaveInput:IDisposable {
 [StructLayout(LayoutKind.Sequential)] struct Format {public ushort tag,channels;public uint rate,avg;public ushort align,bits,extra;}
 [StructLayout(LayoutKind.Sequential)] struct Header {public IntPtr data;public uint len,recorded;public IntPtr user;public uint flags,loops;public IntPtr next,reserved;}
 sealed class BufferSlot {public IntPtr Header,Data;public bool Prepared,Queued;}
 [DllImport("winmm.dll")]static extern int waveInOpen(out IntPtr h,uint id,ref Format f,IntPtr callback,IntPtr instance,uint flags);
 [DllImport("winmm.dll")]static extern int waveInPrepareHeader(IntPtr h,IntPtr p,uint size);
 [DllImport("winmm.dll")]static extern int waveInAddBuffer(IntPtr h,IntPtr p,uint size);
 [DllImport("winmm.dll")]static extern int waveInStart(IntPtr h);
 [DllImport("winmm.dll")]static extern int waveInStop(IntPtr h);
 [DllImport("winmm.dll")]static extern int waveInReset(IntPtr h);
 [DllImport("winmm.dll")]static extern int waveInUnprepareHeader(IntPtr h,IntPtr p,uint size);
 [DllImport("winmm.dll")]static extern int waveInClose(IntPtr h);
 const uint WaveMapper=0xffffffff,CallbackEvent=0x50000,HeaderDone=0x1;
 const int Created=0,Running=1,Draining=2,Cancelling=3,Closed=4;
 readonly object sync=new object();readonly List<BufferSlot> slots=new List<BufferSlot>();
 AutoResetEvent completed;Thread worker;IntPtr handle;int state=Created,nextCompleted;Exception workerFailure;
 public event Action<byte[]> Data;public event Action<double> Level;

 public void Start(){
  lock(sync){if(state!=Created)throw new InvalidOperationException("麦克风采集已经启动或关闭。");}
  completed=new AutoResetEvent(false);
  var format=new Format{tag=1,channels=1,rate=16000,bits=16,align=2,avg=32000};
  int error=waveInOpen(out handle,WaveMapper,ref format,completed.SafeWaitHandle.DangerousGetHandle(),IntPtr.Zero,CallbackEvent);
  if(error!=0){completed.Dispose();completed=null;throw Error("无法打开默认麦克风",error);}
  try{
   uint size=(uint)Marshal.SizeOf(typeof(Header));
   for(int i=0;i<6;i++){
    var slot=new BufferSlot{Data=Marshal.AllocHGlobal(3200),Header=Marshal.AllocHGlobal((int)size)};
    slots.Add(slot);Marshal.StructureToPtr(new Header{data=slot.Data,len=3200},slot.Header,false);
    error=waveInPrepareHeader(handle,slot.Header,size);if(error!=0)throw Error("无法准备麦克风缓冲区",error);slot.Prepared=true;
    error=waveInAddBuffer(handle,slot.Header,size);if(error!=0)throw Error("无法提交麦克风缓冲区",error);slot.Queued=true;
   }
   lock(sync)state=Running;
   worker=new Thread(Worker){IsBackground=true,Name="BanaStudio microphone"};worker.Start();
   error=waveInStart(handle);if(error!=0)throw Error("麦克风启动失败",error);
  }catch{Cancel();throw;}
 }

 public void StopAndDrain(){Stop(true);}
 public void Cancel(){Stop(false);}
 void Stop(bool drain){
  Thread currentWorker;
  lock(sync){
   if(state==Closed)return;
   if(state==Running||state==Created)state=drain?Draining:Cancelling;
   else if(!drain)state=Cancelling;
   currentWorker=worker;
  }
  if(handle!=IntPtr.Zero){waveInStop(handle);waveInReset(handle);}
  if(completed!=null)completed.Set();
  if(currentWorker!=null&&currentWorker!=Thread.CurrentThread&&!currentWorker.Join(5000))throw new TimeoutException("麦克风停止超时。");
  if(currentWorker==Thread.CurrentThread)return;
  CloseNative();
  if(workerFailure!=null&&drain)throw new InvalidOperationException("麦克风数据处理失败。");
 }

 void Worker(){
  try{
   while(true){
   completed.WaitOne();ProcessCompleted();
    bool reset=false;
    lock(sync){if((state==Draining||state==Cancelling)&&slots.All(x=>!x.Queued))break;if(state==Closed)break;reset=state==Cancelling&&slots.Any(x=>x.Queued);}
    if(reset&&handle!=IntPtr.Zero){waveInReset(handle);completed.Set();}
   }
  }catch(Exception error){workerFailure=error;lock(sync){if(state==Running)state=Cancelling;}}
 }
 void ProcessCompleted(){
  while(slots.Count>0){
   BufferSlot slot;
   byte[] data=null;bool deliver=false,requeue=false;
   lock(sync){
    slot=slots[nextCompleted];
    if(!slot.Queued||slot.Header==IntPtr.Zero)break;
    var header=(Header)Marshal.PtrToStructure(slot.Header,typeof(Header));if((header.flags&HeaderDone)==0)break;
    slot.Queued=false;deliver=state!=Cancelling&&header.recorded>0;
    if(deliver){data=new byte[header.recorded];Marshal.Copy(header.data,data,0,data.Length);}
    header.recorded=0;Marshal.StructureToPtr(header,slot.Header,false);requeue=state==Running;
   }
   if(deliver&&data!=null)Publish(data);
   lock(sync){
    if(requeue&&state==Running&&handle!=IntPtr.Zero){int error=waveInAddBuffer(handle,slot.Header,(uint)Marshal.SizeOf(typeof(Header)));if(error==0)slot.Queued=true;else{workerFailure=Error("无法继续麦克风采集",error);state=Cancelling;}}
    nextCompleted=(nextCompleted+1)%slots.Count;
   }
  }
 }
 void Publish(byte[] data){
  int peak=0;for(int i=0;i+1<data.Length;i+=2){int sample=(short)(data[i]|data[i+1]<<8);int amplitude=sample==short.MinValue?32768:Math.Abs(sample);if(amplitude>peak)peak=amplitude;}
  var level=Level;if(level!=null)foreach(Action<double> handler in level.GetInvocationList())try{handler(peak/32768.0);}catch{}
  var output=Data;if(output!=null)foreach(Action<byte[]> handler in output.GetInvocationList())try{handler(data);}catch{}
 }
 void CloseNative(){
  lock(sync){if(state==Closed)return;state=Closed;}
  uint size=(uint)Marshal.SizeOf(typeof(Header));
  bool released=true;
  if(handle!=IntPtr.Zero){
   foreach(var slot in slots)if(slot.Prepared){
    int error=0;for(int attempt=0;attempt<50;attempt++){error=waveInUnprepareHeader(handle,slot.Header,size);if(error==0)break;Thread.Sleep(10);}
    if(error==0)slot.Prepared=false;else{released=false;workerFailure=Error("无法释放麦克风缓冲区",error);}
   }
   if(released){int error=waveInClose(handle);if(error==0)handle=IntPtr.Zero;else{released=false;workerFailure=Error("无法关闭麦克风",error);}}
  }
  if(!released)return;
  foreach(var slot in slots){if(slot.Header!=IntPtr.Zero){Marshal.FreeHGlobal(slot.Header);slot.Header=IntPtr.Zero;}if(slot.Data!=IntPtr.Zero){Marshal.FreeHGlobal(slot.Data);slot.Data=IntPtr.Zero;}}
  slots.Clear();if(completed!=null){completed.Dispose();completed=null;}worker=null;
 }
 static InvalidOperationException Error(string text,int code){return new InvalidOperationException(text+"（"+code+"）。");}
 public void Dispose(){Cancel();}
}
