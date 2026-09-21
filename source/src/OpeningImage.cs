using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace VisualNovelHelper {
public static class OpeningImage {
    // KiriKiri exports BI_RGB BMP32 with meaningful alpha. Windows' BMP decoder
    // can ignore that alpha, making white transparent cards entirely white.
    public static void Prepare(string source,string destination) {
        var bytes=File.ReadAllBytes(source);
        if(bytes.Length<54 || bytes[0]!=66 || bytes[1]!=77 || BitConverter.ToInt32(bytes,14)!=40 || BitConverter.ToInt16(bytes,28)!=32 || BitConverter.ToInt32(bytes,30)!=0)
            throw new InvalidDataException("Unsupported opening image format.");
        int offset=BitConverter.ToInt32(bytes,10),width=BitConverter.ToInt32(bytes,18),signedHeight=BitConverter.ToInt32(bytes,22);
        if(width<1 || width>4096 || signedHeight==0 || signedHeight < -4096 || signedHeight>4096 || offset<54)
            throw new InvalidDataException("Invalid opening image dimensions.");
        int height=Math.Abs(signedHeight),stride=width*4;
        if((long)offset+(long)stride*height>bytes.Length) throw new InvalidDataException("Incomplete opening image.");
        long luminance=0,weight=0;
        for(int i=offset;i<offset+stride*height;i+=4) {int a=bytes[i+3];weight+=a;luminance+=(bytes[i]+bytes[i+1]+bytes[i+2])*a;}
        int background=weight>0 && luminance/weight<384?255:0;
        var pixels=new byte[stride*height];
        for(int y=0;y<height;y++) for(int x=0;x<width;x++) {
            int src=offset+(signedHeight>0?height-1-y:y)*stride+x*4,dst=y*stride+x*4,a=bytes[src+3];
            for(int c=0;c<3;c++) pixels[dst+c]=(byte)((bytes[src+c]*a+background*(255-a))/255);
            pixels[dst+3]=255;
        }
        using(var image=new Bitmap(width,height,PixelFormat.Format32bppArgb)) {
            var data=image.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try {for(int y=0;y<height;y++) Marshal.Copy(pixels,y*stride,IntPtr.Add(data.Scan0,y*data.Stride),stride);}
            finally {image.UnlockBits(data);}
            image.Save(destination,ImageFormat.Png);
        }
    }
}
}
