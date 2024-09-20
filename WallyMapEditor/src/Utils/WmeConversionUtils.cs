using System;
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SwfLib.Data;

namespace WallyMapEditor;

public static partial class WmeUtils
{
    //m11, m12, m13, m14
    //m21, m22, m23, m24
    //m31, m32, m33, m34
    //m41, m42, m43, m44
    public static Matrix4x4 TransformToMatrix4x4(WmsTransform t) => new(
        (float)t.ScaleX, (float)t.SkewY, 0, 0,
        (float)t.SkewX, (float)t.ScaleY, 0, 0,
        0, 0, 1, 0,
        (float)t.TranslateX, (float)t.TranslateY, 0, 1
    );
    public static WmsTransform Matrix4x4ToTransform(Matrix4x4 m) => new(m.M11, m.M21, m.M12, m.M22, m.M41, m.M42);
    public static WmsTransform SwfMatrixToTransform(SwfMatrix m) => new(m.ScaleX, m.RotateSkew1, m.RotateSkew0, m.ScaleY, m.TranslateX / 20.0, m.TranslateY / 20.0);

    public static RlColor WmsColorToRlColor(WmsColor c) => new(c.R, c.G, c.B, c.A);

    public static RlImage ImgSharpImageToRlImage(Image<Rgba32> image)
    {
        RlImage img;
        unsafe
        {
            long bufferSize = (long)image.Width * image.Height * image.PixelType.BitsPerPixel / 8;
            if (bufferSize > int.MaxValue)
            {
                Rl.TraceLog(Raylib_cs.TraceLogLevel.Fatal, $"Image exceeded {int.MaxValue} bytes");
                return default;
            }
            // use Rl alloc so GC doesn't free the memory
            void* bufferPtr = Rl.MemAlloc((uint)bufferSize);
            // create span so ImageSharp can write to the memory
            Span<byte> buffer = new(bufferPtr, (int)bufferSize);
            image.CopyPixelDataTo(buffer);
            img = new()
            {
                Data = bufferPtr,
                Width = image.Width,
                Height = image.Height,
                Mipmaps = 1,
                Format = Raylib_cs.PixelFormat.UncompressedR8G8B8A8,
            };
        }
        return img;
    }

    public static uint RlColorToHex(RlColor color) => (uint)((color.R << 24) | (color.G << 16) | (color.B << 8) | color.A);
    public static RlColor HexToRlColor(uint hex) => new((byte)(hex >> 24), (byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);
    public static RlColor? ParseRlColorOrNull(string? s) => s is null ? null : HexToRlColor(Convert.ToUInt32(s, 16));
}