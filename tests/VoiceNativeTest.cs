using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

static class VoiceNativeTest {
 static int passed,failed;

 [STAThread]
 static int Main(){
  Application.EnableVisualStyles();
  Application.SetCompatibleTextRenderingDefault(false);
  Run("editable target paste and focus safety",RunTargetTest);
  Run("microphone stop drains and cancel returns",RunMicrophoneTest);
  Console.WriteLine("Voice native tests: "+passed+" passed, "+failed+" failed");
  return failed==0?0:1;
 }

 static void Run(string name,Action test){
  try{test();passed++;Console.WriteLine("PASS "+name);}
  catch(Exception error){failed++;Console.WriteLine("FAIL "+name+": "+error.GetType().Name+" - "+error.Message);}
 }

 static void RunTargetTest(){
  Exception failure=null;
  var form=new Form{Text="BanaStudio native target test",Width=460,Height=220,StartPosition=FormStartPosition.CenterScreen,TopMost=true};
  var first=new TextBox{Left=24,Top=24,Width=390,Name="FirstInput"};
  var button=new Button{Left=24,Top=68,Width=150,Text="Non-editable target",Name="TargetButton"};
  var second=new TextBox{Left=24,Top=112,Width=390,Name="SecondInput"};
  form.Controls.Add(first);form.Controls.Add(button);form.Controls.Add(second);
  System.Windows.IDataObject oldClipboard=null;
  bool hadClipboard=false;
  try{oldClipboard=System.Windows.Clipboard.GetDataObject();hadClipboard=oldClipboard!=null;}catch{}
  form.Shown+=async delegate{
   try{
    const string payload="BanaStudio native paste";
    await Focus(form,first);
    VoiceTarget original=await Capture();
    Check(original!=null&&original.StillValid(),"first TextBox was not captured as an editable UIA target");
    bool pasted=await original.Paste(payload,CancellationToken.None);
    await Task.Delay(250);
    Check(pasted,"paste did not submit keyboard input");
    Check(first.Text==payload,"first TextBox did not receive exactly one copy");
    Check(System.Windows.Clipboard.ContainsText()&&System.Windows.Clipboard.GetText()==payload,"clipboard does not contain the pasted text");

    await Focus(form,button);
    Check(!original.StillValid(),"old target remained valid after focus moved to a button");
    Check(!await original.Paste("WRONG-BUTTON",CancellationToken.None),"paste was accepted after focus moved to a button");
    await Task.Delay(100);
    Check(first.Text==payload&&second.Text.Length==0,"button focus test delivered text to an input");
    Check(VoiceTarget.Capture()==null,"button was accepted as an editable UIA target");

    await Focus(form,second);
    VoiceTarget current=await Capture();
    Check(current!=null&&current.StillValid(),"second TextBox was not captured as an editable UIA target");
    Check(!original.StillValid(),"old target remained valid after focus moved to another TextBox");
    Check(!await original.Paste("WRONG-OLD-TARGET",CancellationToken.None),"old target accepted paste after another TextBox received focus");
    await Task.Delay(100);
    Check(first.Text==payload&&second.Text.Length==0,"old target paste was delivered to the wrong TextBox");

    using(var cancelled=new CancellationTokenSource()){
     cancelled.Cancel();
     bool observed=false;
     try{await current.Paste("WRONG-CANCELLED",cancelled.Token);}catch(OperationCanceledException){observed=true;}
     Check(observed,"Paste did not observe a cancelled token");
     await Task.Delay(100);
     Check(second.Text.Length==0,"cancelled paste delivered text");
    }
   }catch(Exception error){failure=error;}
   finally{
    try{if(hadClipboard)System.Windows.Clipboard.SetDataObject(oldClipboard,true);else System.Windows.Clipboard.Clear();}catch{}
    form.Close();
   }
  };
  Application.Run(form);
  if(failure!=null)throw failure;
 }

 static async Task Focus(Form form,Control control){
  uint current=Native.GetCurrentThreadId();
  uint foreground=Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),IntPtr.Zero);
  bool attached=foreground!=0&&foreground!=current&&Native.AttachThreadInput(current,foreground,true);
  try{
   form.Activate();Native.BringWindowToTop(form.Handle);Native.SetForegroundWindow(form.Handle);
   control.Select();control.Focus();Native.SetFocus(control.Handle);
  }finally{if(attached)Native.AttachThreadInput(current,foreground,false);}
  await Task.Delay(180);
 }

 static async Task<VoiceTarget> Capture(){
  for(int i=0;i<12;i++){
   var target=VoiceTarget.Capture();if(target!=null)return target;
   await Task.Delay(100);
  }
  return null;
 }

 static void RunMicrophoneTest(){
  int packets=0;long bytes=0;
  using(var input=new WaveInput()){
   input.Data+=delegate(byte[] data){Interlocked.Increment(ref packets);Interlocked.Add(ref bytes,data.Length);};
   input.Start();Thread.Sleep(1300);input.StopAndDrain();
  }
  Check(packets>0,"StopAndDrain returned no microphone packets");
  Check(bytes>0,"StopAndDrain returned no microphone bytes");

  var timer=Stopwatch.StartNew();
  using(var input=new WaveInput()){
   input.Start();Thread.Sleep(150);input.Cancel();
  }
  timer.Stop();
  Check(timer.Elapsed<TimeSpan.FromSeconds(5),"Cancel did not return within five seconds");
  Console.WriteLine("  microphone packets="+packets+" bytes="+bytes+" cancelMs="+(long)timer.Elapsed.TotalMilliseconds);
 }

 static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}

 static class Native {
  [DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr window,IntPtr process);
  [DllImport("kernel32.dll")]public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")]public static extern bool AttachThreadInput(uint attach,uint attachTo,bool value);
  [DllImport("user32.dll")]public static extern bool BringWindowToTop(IntPtr window);
  [DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr window);
  [DllImport("user32.dll")]public static extern IntPtr SetFocus(IntPtr window);
 }
}
