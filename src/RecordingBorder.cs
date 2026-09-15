using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

static class OverlayNative {
 [DllImport("user32.dll", SetLastError=true)]
 public static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
}

// A hollow, non-activating window. The interior has no window surface at all.
sealed class RecordingBorder : Form {
 public bool ExcludedFromCapture { get; private set; }
 public RecordingBorder(Rectangle area) {
  Text="Banarec · 录制边框"; AutoScaleMode=AutoScaleMode.None;
  FormBorderStyle=FormBorderStyle.None; StartPosition=FormStartPosition.Manual;
  Bounds=area; ShowInTaskbar=false; TopMost=true; BackColor=Color.FromArgb(255,67,87);
  Opacity=0.99;
  var ring=new Region(new Rectangle(Point.Empty,area.Size));
  ring.Exclude(new Rectangle(3,3,Math.Max(1,area.Width-6),Math.Max(1,area.Height-6)));
  Region=ring;
 }
 protected override bool ShowWithoutActivation { get { return true; } }
 protected override CreateParams CreateParams { get { var p=base.CreateParams; p.ExStyle|=0x08000000|0x80|0x20; return p; } }
 protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ExcludedFromCapture=OverlayNative.SetWindowDisplayAffinity(Handle,0x11); }
 public void SetRecording(bool recording) { BackColor=recording?Color.FromArgb(255,67,87):Color.FromArgb(255,187,70); }
}
