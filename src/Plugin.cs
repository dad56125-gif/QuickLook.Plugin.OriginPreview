using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms.Integration;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickLook.Common.Plugin;
[assembly: System.Reflection.AssemblyVersion("1.4.2.0")]
namespace QuickLook.Plugin.OriginPreview {
public sealed class Plugin : IViewer {
 int generation; ScrollViewer viewer; ContentControl body; WindowsFormsHost host; PreviewControl control;
 public int Priority { get { return 100; } }
 string PluginDir { get { return Path.GetDirectoryName(typeof(Plugin).Assembly.Location); } }
 public void Init() {}
 public bool CanHandle(string path) { return File.Exists(path) && string.Equals(Path.GetExtension(path),".opju",StringComparison.OrdinalIgnoreCase); }
 public void Prepare(string path,ContextObject context) { context.SetPreferredSizeFit(new Size(1000,800),0.85); }
 string Setting(string key,string fallback) { try { return (string)XDocument.Load(Path.Combine(PluginDir,"OriginPreview.config")).Root.Element(key) ?? fallback; } catch { return fallback; } }
 static bool Valid(string cache,int edge) {
  try { var r=XDocument.Load(Path.Combine(cache,"manifest.xml")).Root; return (string)r.Attribute("graphPolicy")=="standalone-v1" && (string)r.Attribute("maxEdge")==edge.ToString() && r.Elements("Page").Any() && r.Elements("Page").All(p=>{var f=(string)p.Attribute("file"); return !string.IsNullOrEmpty(f) && Path.GetFileName(f)==f && File.Exists(Path.Combine(cache,f));}); } catch { return false; }
 }
 public void View(string path,ContextObject context) {
  int ticket=++generation;
  var scroll=new ScrollViewer { Background=Brushes.White,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false };
  viewer=scroll; var content=new ContentControl(); body=content; context.ViewerContent=content; context.Title=Path.GetFileName(path); context.IsBusy=false;
  string python=Setting("PythonPath",""); string script=Path.Combine(PluginDir,"generate-preview.py");
  int edge; if(!int.TryParse(Setting("MaxEdge","1000"),out edge)) edge=1000; edge=Math.Max(200,Math.Min(1000,edge));
  var dispatcher=scroll.Dispatcher;
  // Extraction intentionally outlives the preview window.
  Task.Run(()=>{
   try {
    string hash; using(var sha=SHA256.Create()) using(var f=File.OpenRead(path)) hash=BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();
    string cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QuickLook","OriginPreview","cache",hash);
    if(!Valid(cache,edge)) {
     dispatcher.Invoke(new Action(()=>{if(ticket==generation) ShowNative(content,path);}));
     if(!File.Exists(python)) throw new FileNotFoundException("Preview Python runtime unavailable",python);
     using(var process=Process.Start(new ProcessStartInfo(python,"\""+script+"\" \""+path+"\" --width "+edge) {UseShellExecute=false,CreateNoWindow=true})) {
      process.WaitForExit(); if(process.ExitCode!=0) throw new IOException("Preview extraction failed: "+process.ExitCode);
     }
    }
    if(!Valid(cache,edge)) throw new IOException("Preview cache incomplete"); return cache;
   } catch(Exception ex) {
    try { string log=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QuickLook","OriginPreview"); Directory.CreateDirectory(log); File.AppendAllText(Path.Combine(log,"errors.log"),DateTime.Now+" "+ex+Environment.NewLine); } catch {}
    return null;
   }
  }).ContinueWith(t=>{ if(dispatcher.HasShutdownStarted) return; dispatcher.BeginInvoke(new Action(()=>{if(ticket!=generation || t.Result==null) return; try {Display(scroll,t.Result); content.Content=scroll; ReleaseNative();} catch {}})); });
 }
 static void Display(ScrollViewer scroll,string cache) {
  var pages=XDocument.Load(Path.Combine(cache,"manifest.xml")).Root.Elements("Page").ToArray();
  var stack=new StackPanel {Orientation=Orientation.Vertical,HorizontalAlignment=HorizontalAlignment.Center};
  foreach(var page in pages) {
   var bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad; bitmap.UriSource=new Uri(Path.Combine(cache,(string)page.Attribute("file"))); bitmap.EndInit(); bitmap.Freeze();
   var image=new Image {Source=bitmap,Stretch=Stretch.Uniform,Margin=new Thickness(8,8,8,16)};
   var card=new StackPanel(); var label=new TextBlock {Text=(string)page.Attribute("name") ?? "",FontSize=13,Foreground=Brushes.DimGray,Height=20,Margin=new Thickness(8,8,8,0),TextTrimming=TextTrimming.CharacterEllipsis}; card.Children.Add(label); RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality); card.Children.Add(image); stack.Children.Add(card);
  }
  Action resize=()=>{double width=Math.Max(1,scroll.ViewportWidth-16); foreach(StackPanel card in stack.Children) {var image=(Image)card.Children[1]; var bitmap=(BitmapSource)image.Source; double w=Math.Min(width,bitmap.PixelWidth); w=Math.Min(w,Math.Max(1,scroll.ViewportHeight-52)*bitmap.PixelWidth/bitmap.PixelHeight); card.Width=w+16; image.Width=w; image.Height=w*bitmap.PixelHeight/bitmap.PixelWidth;}};
  scroll.ScrollChanged+=delegate(object sender,ScrollChangedEventArgs e){if(e.ViewportWidthChange!=0 || e.ViewportHeightChange!=0) resize();};
  scroll.Content=stack; resize();
 }
 void ReleaseNative() {if(host!=null){host.Child=null;host.Dispose();host=null;} if(control!=null){control.Dispose();control=null;}}
 void ShowNative(ContentControl content,string path) {try {control=new PreviewControl(); host=new WindowsFormsHost {Child=control}; var current=control; host.Loaded+=delegate {try {if(control==current) current.Open(path);} catch {}}; content.Content=host;} catch {ReleaseNative();}}
 public void Cleanup() {++generation; ReleaseNative(); if(body!=null){body.Content=null;body=null;} if(viewer!=null){viewer.Content=null; viewer=null;}}
}
    internal sealed class PreviewControl : System.Windows.Forms.Control
    {
        private object instance;
        private IPreviewHandler preview;
        private IStream stream;
        private bool opened;
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateStreamOnFileEx(string path, uint mode, uint attributes, bool create, IStream template, out IStream result);
        public void Open(string path)
        {
            if (opened) return;
            opened = true;
            using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(@".opju\ShellEx\{8895b1c6-b41f-4c1c-a562-0d564250836f}"))
            {
                string value = key == null ? null : key.GetValue(null) as string;
                if (string.IsNullOrEmpty(value)) throw new InvalidOperationException("未注册 Origin OPJU 预览处理器。");
                instance = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid(value), true));
            }
            preview = (IPreviewHandler)instance;
            var fileInit = instance as IInitializeWithFile;
            if (fileInit != null) fileInit.Initialize(path, 0);
            else
            {
                var streamInit = instance as IInitializeWithStream;
                if (streamInit == null) throw new NotSupportedException("Origin 处理器不支持文件或流初始化。");
                SHCreateStreamOnFileEx(path, 0x20, 0, false, null, out stream);
                streamInit.Initialize(stream, 0);
            }
            var rect = new RECT(Width, Height);
            preview.SetWindow(Handle, ref rect);
            preview.DoPreview();
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (preview != null) { try { var rect = new RECT(Width, Height); preview.SetRect(ref rect); } catch (COMException) { } }
        }
        protected override void Dispose(bool disposing)
        {
            if (preview != null) { try { preview.Unload(); } catch (COMException) { } preview = null; }
            if (instance != null) { Marshal.FinalReleaseComObject(instance); instance = null; }
            if (stream != null) { Marshal.FinalReleaseComObject(stream); stream = null; }
            base.Dispose(disposing);
        }
    }
    [StructLayout(LayoutKind.Sequential)] internal struct RECT
    {
        public int Left, Top, Right, Bottom;
        public RECT(int width, int height) { Left = Top = 0; Right = width; Bottom = height; }
    }
    [ComImport, Guid("8895b1c6-b41f-4c1c-a562-0d564250836f"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPreviewHandler
    {
        void SetWindow(IntPtr hwnd, ref RECT rect);
        void SetRect(ref RECT rect);
        void DoPreview();
        void Unload();
        void SetFocus();
        void QueryFocus(out IntPtr hwnd);
        [PreserveSig] uint TranslateAccelerator(IntPtr message);
    }
    [ComImport, Guid("b7d14566-0509-4cce-a71f-0a554233bd9b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IInitializeWithFile { void Initialize([MarshalAs(UnmanagedType.LPWStr)] string path, uint mode); }
    [ComImport, Guid("b824b49d-22ac-4161-ac8a-9916e8fa3f7f"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IInitializeWithStream { void Initialize(IStream stream, uint mode); }
}

