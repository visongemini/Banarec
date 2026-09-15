using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ScreenRecorderLib;

static class Native {
 [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
 [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
 [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("shell32.dll")] static extern int SHGetKnownFolderPath(ref Guid id,uint flags,IntPtr token,out IntPtr path);
 public static string Downloads(){Guid id=new Guid("374DE290-123F-4565-9164-39C4925E467B");IntPtr p;int hr=SHGetKnownFolderPath(ref id,0,IntPtr.Zero,out p);if(hr!=0)Marshal.ThrowExceptionForHR(hr);try{return Marshal.PtrToStringUni(p);}finally{Marshal.FreeCoTaskMem(p);}}
}
sealed class Capture : IDisposable {
 public Recorder Rec; public event Action<string> Completed; public event Action<string> Failed; public event Action Started;
 public static Rectangle Normalize(Rectangle r,Rectangle bounds){r=Rectangle.Intersect(r,bounds);r.Width-=r.Width%2;r.Height-=r.Height%2;if(r.Width<16||r.Height<16)throw new Exception("录制区域至少需要 16 × 16 像素，请重新框选。");return r;}
 public void Start(Screen screen,Rectangle area,bool system,bool mic,int fps,string path,bool hardware){
  area=Normalize(area,screen.Bounds);
  var audio=new List<AudioSourceBase>();
  if(system){if(Recorder.GetSystemAudioLoopbackDevices().Count==0)throw new Exception("没有可用的扬声器或耳机，请连接声音输出设备后重试。");audio.Add(LoopbackAudioSource.Default);}
  if(mic){if(Recorder.GetSystemAudioCaptureDevices().Count==0)throw new Exception("没有可用的麦克风，请检查设备和 Windows 麦克风权限。");audio.Add(CaptureAudioSource.Default);}
  var source=new DisplayRecordingSource(screen.DeviceName){SourceRect=new ScreenRect(area.X-screen.Bounds.X,area.Y-screen.Bounds.Y,area.Width,area.Height),RecorderApi=RecorderApi.DesktopDuplication};
  var opts=RecorderOptions.Default;
  opts.SourceOptions=new SourceOptions{RecordingSources=new List<RecordingSourceBase>{source}};
  opts.AudioOptions=new AudioOptions{IsAudioEnabled=audio.Count>0,AudioSources=audio,Bitrate=AudioBitrate.bitrate_192kbps};
  opts.VideoEncoderOptions=new VideoEncoderOptions{Framerate=fps,Quality=90,IsFixedFramerate=true,IsHardwareEncodingEnabled=hardware,IsMp4FastStartEnabled=true,Encoder=new H264VideoEncoder{BitrateMode=H264BitrateControlMode.Quality}};
  opts.MouseOptions=new MouseOptions{IsMousePointerEnabled=true};
  Rec=Recorder.CreateRecorder(opts);
  Rec.OnRecordingComplete+=(s,e)=>{if(Completed!=null)Completed(e.FilePath);};
  Rec.OnRecordingFailed+=(s,e)=>{if(Failed!=null)Failed(e.Error);};
  Rec.OnStatusChanged+=(s,e)=>{if(e.Status==RecorderStatus.Recording&&Started!=null)Started();};
  Rec.Record(path);
 }
 public void Stop(){if(Rec!=null)Rec.Stop();}
 public void Dispose(){if(Rec!=null){Rec.Dispose();Rec=null;}}
}
sealed class RegionPicker:Form {
 Point origin;Rectangle selection;bool dragging;Bitmap shot;
 public Rectangle Selected;
 public RegionPicker(Screen screen):this(screen.Bounds,false){}
 bool screenshotMode;
 public Bitmap GetSelectionImage(){return shot.Clone(new Rectangle(Selected.X-Left,Selected.Y-Top,Selected.Width,Selected.Height),System.Drawing.Imaging.PixelFormat.Format32bppArgb);}
 public RegionPicker(Rectangle bounds,bool screenshot){screenshotMode=screenshot;AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Bounds=bounds;TopMost=true;DoubleBuffered=true;KeyPreview=true;Cursor=Cursors.Cross;
  shot=new Bitmap(Width,Height);using(var g=Graphics.FromImage(shot))g.CopyFromScreen(Bounds.Location,Point.Empty,Bounds.Size);
  KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){DialogResult=DialogResult.Cancel;Close();}};
  MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Right){Close();return;}if(e.Button!=MouseButtons.Left)return;dragging=true;origin=e.Location;Capture=true;};
  MouseMove+=(s,e)=>{if(!dragging)return;var p=new Point(Math.Max(0,Math.Min(Width,e.X)),Math.Max(0,Math.Min(Height,e.Y)));selection=Rectangle.FromLTRB(Math.Min(origin.X,p.X),Math.Min(origin.Y,p.Y),Math.Max(origin.X,p.X),Math.Max(origin.Y,p.Y));Invalidate();};
  MouseUp+=(s,e)=>{if(!dragging)return;dragging=false;Capture=false;if(selection.Width<(screenshotMode?1:16)||selection.Height<(screenshotMode?1:16)){selection=Rectangle.Empty;Invalidate();return;}Selected=new Rectangle(selection.X+Left,selection.Y+Top,screenshotMode?selection.Width:selection.Width&~1,screenshotMode?selection.Height:selection.Height&~1);DialogResult=DialogResult.OK;Close();};
 }
 protected override void OnPaint(PaintEventArgs e){e.Graphics.DrawImageUnscaled(shot,0,0);using(var dim=new SolidBrush(Color.FromArgb(145,0,0,0)))e.Graphics.FillRectangle(dim,ClientRectangle);if(!selection.IsEmpty){e.Graphics.DrawImage(shot,selection,selection,GraphicsUnit.Pixel);using(var pen=new Pen(Color.FromArgb(71,220,175),2))e.Graphics.DrawRectangle(pen,selection);}
  string text=selection.IsEmpty?(screenshotMode?"拖动截取画面 · Esc / 右键取消":"拖动框选录制区域 · Esc / 右键取消"):selection.Width+" × "+selection.Height+" 像素 · 松开鼠标确认";
  using(var font=new Font("Microsoft YaHei UI",14))using(var b=new SolidBrush(Color.FromArgb(230,20,28,40))){var size=e.Graphics.MeasureString(text,font);e.Graphics.FillRectangle(b,24,24,size.Width+32,52);e.Graphics.DrawString(text,font,Brushes.White,40,36);}
 }
 protected override void Dispose(bool d){if(d&&shot!=null)shot.Dispose();base.Dispose(d);}
}
sealed class Countdown:Form {
 public Label Number;
 public Countdown(Rectangle region){AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Size=new Size(150,150);Location=new Point(region.X+(region.Width-Width)/2,region.Y+(region.Height-Height)/2);BackColor=Color.FromArgb(20,29,43);TopMost=true;Number=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.White,Font=new Font("Segoe UI",58,FontStyle.Bold),Text="3"};Controls.Add(Number);}
}
