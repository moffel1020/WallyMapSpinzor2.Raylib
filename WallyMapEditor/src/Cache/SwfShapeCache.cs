using System;
using System.Numerics;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using SwiffCheese.Exporting;
using SwiffCheese.Shapes;
using SwiffCheese.Utils;
using SwiffCheese.Wrappers;

using Raylib_cs;

namespace WallyMapEditor;

public class SwfShapeCache : UploadCache<SwfShapeCache.TextureInfo, SwfShapeCache.ShapeData, Texture2DWrapper>
{
    private const int RASTER_SCALE = 1;
    private const int SWF_UNIT_DIVISOR = 20;
    private const double ANIM_SCALE_MULTIPLIER = 1.2;

    public readonly record struct TextureInfo(SwfFileData Swf, ushort ShapeId, double AnimScale);
    public readonly record struct ShapeData(RlImage Img, int OffsetX, int OffsetY);

    protected override ShapeData LoadIntermediate(TextureInfo textureInfo)
    {
        (SwfFileData swf, ushort shapeId, double animScale) = textureInfo;
        animScale *= ANIM_SCALE_MULTIPLIER;
        DefineShapeXTag shape = swf.ShapeTags[shapeId];
        SwfShape compiledShape = new(shape);
        // logic follows game
        double x = shape.ShapeBounds.XMin * 1.0 / SWF_UNIT_DIVISOR;
        double y = shape.ShapeBounds.YMin * 1.0 / SWF_UNIT_DIVISOR;
        double w = shape.ShapeBounds.Width() * animScale / SWF_UNIT_DIVISOR;
        double h = shape.ShapeBounds.Height() * animScale / SWF_UNIT_DIVISOR;
        int offsetX = (int)Math.Floor(x);
        int offsetY = (int)Math.Floor(y);
        Vector2 offset = new(-offsetX, -offsetY);
        int imageW = (int)Math.Floor(w + (x - offsetX) + animScale) + 2;
        int imageH = (int)Math.Floor(h + (y - offsetY) + animScale) + 2;

        RlImage img;
        using (Image<Rgba32> image = new(RASTER_SCALE * imageW, RASTER_SCALE * imageH, new Rgba32(255, 255, 255, 0)))
        {
            ImageSharpShapeExporter exporter = new(image, RASTER_SCALE * offset, 1f * SWF_UNIT_DIVISOR / RASTER_SCALE);
            compiledShape.Export(exporter);
            image.Mutate(ctx => ctx.Resize(imageW, imageH));
            img = WmeUtils.ImgSharpImageToRlImage(image);
        }
        Rl.ImageAlphaPremultiply(ref img);
        return new ShapeData(img, offsetX, offsetY);
    }

    protected override Texture2DWrapper IntermediateToValue(ShapeData shapeData)
    {
        (RlImage img, int offsetX, int offsetY) = shapeData;
        Texture2D texture = Rl.LoadTextureFromImage(img);
        Rl.SetTextureFilter(texture, TextureFilter.Bilinear);
        return new(texture, offsetX, offsetY);
    }

    protected override void UnloadIntermediate(ShapeData shapeData)
    {
        Rl.UnloadImage(shapeData.Img);
    }

    protected override void UnloadValue(Texture2DWrapper texture)
    {
        texture.Dispose();
    }

    public void Load(SwfFileData swf, ushort shapeId, double animScale) => Load(new(swf, shapeId, animScale));
    public void LoadInThread(SwfFileData swf, ushort shapeId, double animScale) => LoadInThread(new(swf, shapeId, animScale));
}