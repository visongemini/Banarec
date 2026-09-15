using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing=System.Drawing;

sealed class AnnotationWindow : Window {
 public readonly InkCanvas Ink;
 readonly Grid canvas;
 readonly Stack<StrokeCollection> undo=new Stack<StrokeCollection>(),redo=new Stack<StrokeCollection>();
 StrokeCollection before=new StrokeCollection();bool restoring;
 readonly Button undoButton,redoButton;readonly Slider width;
 readonly int pixelsWide,pixelsHigh;string tool="pen";Color color=Color.FromRgb(234,75,75);
 readonly Dictionary<Color,RadioButton> colors=new Dictionary<Color,RadioButton>();
 public Drawing.Bitmap Result {get;private set;}
 public AnnotationWindow(Drawing.Bitmap image,ResourceDictionary resources) {
  Resources=resources;Title="Banarec · 截图标注";FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");FontSize=13;
  WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
  Width=Math.Min(1100,SystemParameters.WorkArea.Width-36);Height=Math.Min(820,SystemParameters.WorkArea.Height-36);WindowStartupLocation=WindowStartupLocation.CenterScreen;
  pixelsWide=image.Width;pixelsHigh=image.Height;
  var shell=new Border{Margin=new Thickness(8),CornerRadius=new CornerRadius(20),Background=Brush("#F8F8FA"),BorderBrush=Brush("#DEDEE3"),BorderThickness=new Thickness(1)};
  var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(64)});layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(62)});layout.RowDefinitions.Add(new RowDefinition());layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(34)});shell.Child=layout;Content=shell;
  var header=new Grid{Margin=new Thickness(23,0,18,0),Background=Brushes.Transparent};header.MouseLeftButtonDown+=(s,e)=>{if(e.OriginalSource is Grid||e.OriginalSource is TextBlock)try{DragMove();}catch{}};
  header.Children.Add(new TextBlock{Text="截图标注",FontSize=19,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,Foreground=Brush("#2F3035")});
  var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};var cancel=new Button{Content="取消",Background=Brushes.Transparent};cancel.Click+=(s,e)=>{DialogResult=false;};actions.Children.Add(cancel);
  var done=new Button{Content="完成并复制",Margin=new Thickness(8,0,0,0),Background=Brush("#ECC451"),Foreground=Brush("#493B18")};done.Click+=(s,e)=>Complete();actions.Children.Add(done);header.Children.Add(actions);layout.Children.Add(header);
  var tools=new WrapPanel{Margin=new Thickness(20,0,20,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetRow(tools,1);layout.Children.Add(tools);
  var segments=new StackPanel{Orientation=Orientation.Horizontal,Background=Brush("#ECECF0"),Margin=new Thickness(0,0,12,0)};
  foreach(string name in new[]{"画笔","荧光笔","橡皮擦"}){string mode=name=="画笔"?"pen":name=="荧光笔"?"highlight":"erase";var b=new RadioButton{Content=name,Style=(Style)resources["Segment"],GroupName="InkTool",IsChecked=mode=="pen"};b.Checked+=(s,e)=>SetTool(mode);segments.Children.Add(b);}tools.Children.Add(segments);
  var colorStyle=(Style)System.Windows.Markup.XamlReader.Parse("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='RadioButton'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RadioButton'><Border x:Name='Ring' BorderBrush='Transparent' BorderThickness='2' CornerRadius='17' Padding='3'><Ellipse Width='20' Height='20' Fill='{TemplateBinding Background}' Stroke='#DDDEE2' StrokeThickness='0.6'/></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Ring' Property='BorderBrush' Value='#6A6B73'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
  foreach(string value in new[]{"#EA4B4B","#F3CA4D","#4BA97C","#548DE5","#AF72CB","#303238","#FFFFFF"}){Color selected=(Color)ColorConverter.ConvertFromString(value);var b=new RadioButton{Style=colorStyle,GroupName="InkColor",Margin=new Thickness(1),Background=Brush(value),ToolTip=value,Cursor=Cursors.Hand};colors.Add(selected,b);b.Checked+=(s,e)=>SetColor(selected);tools.Children.Add(b);}
  colors[color].IsChecked=true;
  tools.Children.Add(new TextBlock{Text="粗细",Foreground=Brush("#93949B"),Margin=new Thickness(15,0,7,0),VerticalAlignment=VerticalAlignment.Center});width=new Slider{Minimum=2,Maximum=24,Value=4,Width=82,VerticalAlignment=VerticalAlignment.Center,ToolTip="笔迹宽度（像素）"};width.ValueChanged+=(s,e)=>UpdatePen();tools.Children.Add(width);
  undoButton=new Button{Content="撤销",Padding=new Thickness(10,8,10,8),Margin=new Thickness(12,0,4,0)};undoButton.Click+=(s,e)=>Undo();tools.Children.Add(undoButton);redoButton=new Button{Content="重做",Padding=new Thickness(10,8,10,8)};redoButton.Click+=(s,e)=>Redo();tools.Children.Add(redoButton);
  var center=new Border{Margin=new Thickness(20,4,20,0),Background=Brush("#EDEDF1"),CornerRadius=new CornerRadius(12),Padding=new Thickness(14)};Grid.SetRow(center,2);layout.Children.Add(center);
  var view=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly};center.Child=view;
  canvas=new Grid{Width=pixelsWide,Height=pixelsHigh,ClipToBounds=true};view.Child=canvas;
  BitmapImage source;using(var stream=new MemoryStream()){image.Save(stream,Drawing.Imaging.ImageFormat.Png);stream.Position=0;source=new BitmapImage();source.BeginInit();source.CacheOption=BitmapCacheOption.OnLoad;source.StreamSource=stream;source.EndInit();source.Freeze();}
  canvas.Children.Add(new Image{Source=source,Width=pixelsWide,Height=pixelsHigh});Ink=new InkCanvas{Background=Brushes.Transparent,Width=pixelsWide,Height=pixelsHigh,ClipToBounds=true,EditingMode=InkCanvasEditingMode.Ink};canvas.Children.Add(Ink);
  Ink.Strokes.StrokesChanged+=(s,e)=>{if(restoring)return;undo.Push(before.Clone());redo.Clear();before=Ink.Strokes.Clone();UpdateUndo();};
  var footer=new TextBlock{Text=pixelsWide+" × "+pixelsHigh+" px   ·   Ctrl+Z 撤销   ·   Ctrl+Y 重做   ·   Esc 取消",FontSize=11,Foreground=Brush("#919299"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};Grid.SetRow(footer,3);layout.Children.Add(footer);
  PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){DialogResult=false;e.Handled=true;}else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.Z){Undo();e.Handled=true;}else if(Keyboard.Modifiers==ModifierKeys.Control&&e.Key==Key.Y){Redo();e.Handled=true;}};
  UpdatePen();UpdateUndo();
 }
 static Brush Brush(string value){return (Brush)new BrushConverter().ConvertFromString(value);}
 public void SetTool(string mode){tool=mode;if(mode=="highlight")SetColor(Color.FromRgb(243,202,77));UpdatePen();}
 public void SetColor(Color value){color=value;RadioButton selected;if(colors.TryGetValue(value,out selected)&&selected.IsChecked!=true)selected.IsChecked=true;UpdatePen();}
 void UpdatePen(){if(Ink==null||width==null)return;Ink.EditingMode=tool=="erase"?InkCanvasEditingMode.EraseByStroke:InkCanvasEditingMode.Ink;double size=tool=="highlight"?Math.Max(12,width.Value):width.Value;Ink.DefaultDrawingAttributes=new DrawingAttributes{Color=color,Width=size,Height=size,IsHighlighter=tool=="highlight",FitToCurve=true,IgnorePressure=true};}
 void UpdateUndo(){undoButton.IsEnabled=undo.Count>0;redoButton.IsEnabled=redo.Count>0;}
 void Restore(StrokeCollection strokes){restoring=true;try{Ink.Strokes.Clear();Ink.Strokes.Add(strokes.Clone());before=Ink.Strokes.Clone();}finally{restoring=false;}UpdateUndo();}
 public void Undo(){if(undo.Count==0)return;redo.Push(Ink.Strokes.Clone());Restore(undo.Pop());}
 public void Redo(){if(redo.Count==0)return;undo.Push(Ink.Strokes.Clone());Restore(redo.Pop());}
 public void Complete(){Result=Export();DialogResult=true;}
 public Drawing.Bitmap Export(){canvas.Measure(new Size(pixelsWide,pixelsHigh));canvas.Arrange(new Rect(0,0,pixelsWide,pixelsHigh));canvas.UpdateLayout();var target=new RenderTargetBitmap(pixelsWide,pixelsHigh,96,96,PixelFormats.Pbgra32);target.Render(canvas);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(target));using(var stream=new MemoryStream()){encoder.Save(stream);stream.Position=0;using(var bitmap=new Drawing.Bitmap(stream))return new Drawing.Bitmap(bitmap);}}
}
