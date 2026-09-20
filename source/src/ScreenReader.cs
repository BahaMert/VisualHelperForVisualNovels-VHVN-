using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace VisualNovelHelper {
public sealed class RecognizedLine { public string Text; public double Left,Top,Right,Bottom; }
public static class ScreenReader {
    static async Task<T> WaitFor<T>(Windows.Foundation.IAsyncOperation<T> operation) {
        var deadline=DateTime.UtcNow.AddSeconds(10);
        while(operation.Status==Windows.Foundation.AsyncStatus.Started) {
            if(DateTime.UtcNow>deadline) { operation.Cancel(); throw new TimeoutException("Screen reading timed out."); }
            await Task.Delay(20);
        }
        try { return operation.GetResults(); } finally { operation.Close(); }
    }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct PointNative { public int X,Y; }
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr w,out Rect rect);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr w,ref PointNative point);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out PointNative point);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr w,IntPtr hdc,uint flags);
    public static async Task<RecognizedLine[]> RecognizeFile(string path) {
        var engine=OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
        if(engine==null) engine=OcrEngine.TryCreateFromUserProfileLanguages();
        if(engine==null) throw new InvalidOperationException("Install a Windows OCR language to use screen reading.");
        var file=await WaitFor<StorageFile>(StorageFile.GetFileFromPathAsync(path));
        using(var stream=await WaitFor<IRandomAccessStream>(file.OpenAsync(FileAccessMode.Read))) {
            var decoder=await WaitFor<BitmapDecoder>(BitmapDecoder.CreateAsync(stream));
            using(var bitmap=await WaitFor<SoftwareBitmap>(decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied))) {
                var result=await WaitFor<OcrResult>(engine.RecognizeAsync(bitmap));
                return result.Lines.Where(l=>l.Words.Count>0).Select(l=>new RecognizedLine {
                    Text=l.Text,Left=l.Words.Min(w=>w.BoundingRect.Left),Top=l.Words.Min(w=>w.BoundingRect.Top),Right=l.Words.Max(w=>w.BoundingRect.Right),Bottom=l.Words.Max(w=>w.BoundingRect.Bottom)
                }).ToArray();
            }
        }
    }
    public static async Task<string> ReadWindow(IntPtr window,bool underPointer) {
        Rect rect; PointNative origin=new PointNative(), pointer;
        if(!GetClientRect(window,out rect) || !ClientToScreen(window,ref origin) || rect.Right<=0 || rect.Bottom<=0) throw new InvalidOperationException("Game window is unavailable for screen reading.");
        GetCursorPos(out pointer);
        var path=Path.Combine(Path.GetTempPath(),"vnh-ocr-"+Guid.NewGuid().ToString("N")+".png");
        try {
            using(var bitmap=new Bitmap(rect.Right,rect.Bottom)) {
                using(var graphics=Graphics.FromImage(bitmap)) {
                    // Capture only game client pixels. PrintWindow handles overlap without reading another app.
                    var dc=graphics.GetHdc(); bool ok;
                    try { ok=PrintWindow(window,dc,3); } finally { graphics.ReleaseHdc(dc); }
                    if(!ok) throw new InvalidOperationException("Game window capture failed. Try windowed mode.");
                }
                bitmap.Save(path,ImageFormat.Png);
            }
            var lines=await RecognizeFile(path);
            if(underPointer) {
                double x=pointer.X-origin.X,y=pointer.Y-origin.Y;
                var near=lines.Where(l=>y>=l.Top-12 && y<=l.Bottom+12 && x>=l.Left-30 && x<=l.Right+30).OrderBy(l=>Math.Abs((l.Top+l.Bottom)/2-y)).FirstOrDefault();
                return near==null?"No readable text under the pointer.":near.Text;
            }
            return lines.Length==0?"No readable text found.":String.Join(". ",lines.Select(l=>l.Text));
        } finally { if(File.Exists(path)) File.Delete(path); }
    }
}
}
