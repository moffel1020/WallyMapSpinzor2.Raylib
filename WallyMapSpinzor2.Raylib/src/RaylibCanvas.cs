using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using Rl = Raylib_cs.Raylib;
using Raylib_cs;

using WallyAnmSpinzor;

using SwiffCheese.Wrappers;

using SwfLib.Tags;

namespace WallyMapSpinzor2.Raylib;

public partial class RaylibCanvas : ICanvas
{
    private readonly string brawlPath;
    public string[] BoneNames { get; set; }

    public BucketPriorityQueue<(object?, Action)> DrawingQueue { get; } = new(Enum.GetValues<DrawPriorityEnum>().Length);
    public TextureCache TextureCache { get; } = new();
    public SwfFileCache SwfFileCache { get; } = new();
    public SwfShapeCache SwfShapeCache { get; } = new();
    public SwfSpriteCache SwfSpriteCache { get; } = new();
    public ShaderCache ShaderCache { get; } = new();
    public ConcurrentDictionary<string, AnmClass> AnmClasses { get; set; } = [];
    public Matrix4x4 CameraMatrix { get; set; } = Matrix4x4.Identity;

    public RaylibCanvas(string brawlPath, string[] boneNames)
    {
        this.brawlPath = brawlPath;
        this.BoneNames = boneNames;
        LoadAnm("MapArtAnims");
        LoadAnm("ATLA_MapArtAnims");
        LoadAnm("GameModes");
    }

    private void LoadAnm(string name)
    {
        Task.Run(() =>
        {
            string anmPath = Path.Combine(brawlPath, "anims", $"Animation_{name}.anm");
            AnmFile anm;
            using (FileStream file = new(anmPath, FileMode.Open, FileAccess.Read))
                anm = AnmFile.CreateFrom(file);
            foreach ((string className, AnmClass @class) in anm.Classes)
            {
                AnmClasses[className] = @class;
            }
        });
    }

    public void ClearTextureCache()
    {
        TextureCache.Clear();
        SwfShapeCache.Clear();
        SwfSpriteCache.Clear();
        SwfFileCache.Clear();
        ShaderCache.Clear();
    }

    public void DrawCircle(double x, double y, double radius, Color color, Transform trans, DrawPriorityEnum priority, object? caller)
    {
        // FIXME: doesn't account for transformations affecting radius (could be turned into an ellipse)
        (x, y) = trans * (x, y);

        DrawingQueue.Push((caller, () =>
        {
            Rl.DrawCircle((int)x, (int)y, (float)radius, Utils.ToRlColor(color));
        }
        ), (int)priority);
    }

    public void DrawLine(double x1, double y1, double x2, double y2, Color color, Transform trans, DrawPriorityEnum priority, object? caller)
    {
        (x1, y1) = trans * (x1, y1);
        (x2, y2) = trans * (x2, y2);

        DrawingQueue.Push((caller, () =>
        {
            Rl.DrawLine((int)x1, (int)y1, (int)x2, (int)y2, Utils.ToRlColor(color));
        }
        ), (int)priority);
    }

    public void DrawLineMultiColor(double x1, double y1, double x2, double y2, Color[] colors, Transform trans, DrawPriorityEnum priority, object? caller)
    {
        if (!Matrix4x4.Invert(CameraMatrix, out Matrix4x4 invertedMat))
            throw new ArgumentException("Camera transform is not invertible");
        Transform cam = Utils.MatrixToTransform(CameraMatrix);
        Transform inv = Utils.MatrixToTransform(invertedMat);

        (x1, y1) = cam * trans * (x1, y1);
        (x2, y2) = cam * trans * (x2, y2);
        if (x1 > x2)
        {
            (x1, x2) = (x2, x1);
            (y1, y2) = (y2, y1);
        }
        double center = (colors.Length - 1) / 2.0;
        float baseOffset = Rlgl.GetLineWidth();
        (double offX, double offY) = (y1 - y2, x2 - x1);
        (offX, offY) = BrawlhallaMath.Normalize(offX, offY);
        for (int i = 0; i < colors.Length; ++i)
        {
            double mult = baseOffset * (i - center);
            DrawLine(x1 + offX * mult, y1 + offY * mult, x2 + offX * mult, y2 + offY * mult, colors[i], inv, priority, caller);
        }
    }

    public void DrawRect(double x, double y, double w, double h, bool filled, Color color, Transform trans, DrawPriorityEnum priority, object? caller)
    {
        DrawingQueue.Push((caller, () =>
        {
            if (filled)
            {
                DrawRectWithTransform(x, y, w, h, trans, color);
            }
            else
            {
                DrawLine(x, y, x + w, y, color, trans, priority, caller);
                DrawLine(x + w, y, x + w, y + h, color, trans, priority, caller);
                DrawLine(x + w, y + h, x, y + h, color, trans, priority, caller);
                DrawLine(x, y + h, x, y, color, trans, priority, caller);
            }
        }
        ), (int)priority);
    }

    public void DrawString(double x, double y, string text, double fontSize, Color color, Transform trans, DrawPriorityEnum priority, object? caller)
    {

    }

    public void DrawTexture(string path, double x, double y, Transform trans, DrawPriorityEnum priority, object? caller)
    {
        Shader defaultShader = LoadShader(null, null);
        Texture2DWrapper texture = LoadTextureFromPath(path);
        DrawingQueue.Push((caller, () =>
        {
            DrawTextureWithTransform(texture.Texture, defaultShader, x + texture.XOff, y + texture.YOff, texture.W, texture.H, trans, 1);
        }
        ), (int)priority);
    }

    public void DrawTextureRect(string path, double x, double y, double? w, double? h, Transform trans, DrawPriorityEnum priority, object? caller)
    {
        Shader defaultShader = LoadShader(null, null);
        Texture2DWrapper texture = LoadTextureFromPath(path);
        w ??= texture.Texture.Width;
        h ??= texture.Texture.Height;
        DrawingQueue.Push((caller, () =>
        {
            DrawTextureWithTransform(texture.Texture, defaultShader, x + texture.XOff, y + texture.YOff, w.Value, h.Value, trans, 1);
        }
        ), (int)priority);
    }

    private static void DrawTextureWithTransform(Texture2D texture, Shader shader, double x, double y, double w, double h, Transform trans, double opacity)
    {
        Rl.BeginBlendMode(BlendMode.AlphaPremultiply);
        Rl.BeginShaderMode(shader);
        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4f((float)opacity, (float)opacity, (float)opacity, (float)opacity);
        (double xMin, double yMin) = (x, y);
        (double xMax, double yMax) = (x + w, y + h);
        (double, double)[] texCoords = [(0, 0), (0, 1), (1, 1), (1, 0), (0, 0)];
        (double, double)[] points = [trans * (xMin, yMin), trans * (xMin, yMax), trans * (xMax, yMax), trans * (xMax, yMin), trans * (xMin, yMin)];
        // raylib requires that the points be in counterclockwise order
        if (Utils.IsPolygonClockwise(points))
        {
            Array.Reverse(texCoords);
            Array.Reverse(points);
        }
        for (int i = 0; i < points.Length - 1; ++i)
        {
            Rlgl.TexCoord2f((float)texCoords[i].Item1, (float)texCoords[i].Item2);
            Rlgl.Vertex2f((float)points[i].Item1, (float)points[i].Item2);
            Rlgl.TexCoord2f((float)texCoords[i + 1].Item1, (float)texCoords[i + 1].Item2);
            Rlgl.Vertex2f((float)points[i + 1].Item1, (float)points[i + 1].Item2);
        }
        Rlgl.End();
        Rlgl.SetTexture(0);
        Rl.EndShaderMode();
        Rl.EndBlendMode();
    }

    private static void DrawRectWithTransform(double x, double y, double w, double h, Transform trans, Color color)
    {
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(color.R, color.G, color.B, color.A);
        (double xMin, double yMin) = (x, y);
        (double xMax, double yMax) = (x + w, y + h);
        (double, double)[] points = [trans * (xMin, yMin), trans * (xMin, yMax), trans * (xMax, yMax), trans * (xMax, yMin), trans * (xMin, yMin)];
        // raylib requires that the points be in counterclockwise order
        if (Utils.IsPolygonClockwise(points))
        {
            Array.Reverse(points);
        }
        for (int i = 0; i < points.Length - 1; ++i)
        {
            Rlgl.Vertex2f((float)points[i].Item1, (float)points[i].Item2);
            Rlgl.Vertex2f((float)points[i + 1].Item1, (float)points[i + 1].Item2);
        }
        Rlgl.End();
    }

    public Texture2DWrapper LoadTextureFromPath(string path)
    {
        string finalPath = Path.Combine(brawlPath, "mapArt", path);
        TextureCache.Cache.TryGetValue(finalPath, out Texture2DWrapper? texture);
        if (texture is not null) return texture;

        TextureCache.LoadAsync(finalPath);
        return Texture2DWrapper.Default; // placeholder white texture until the image is read from disk
    }

    public SwfFileData? LoadSwf(string filePath)
    {
        string finalPath = Path.Combine(brawlPath, filePath);
        SwfFileCache.Cache.TryGetValue(finalPath, out SwfFileData? swf);
        if (swf is not null)
            return swf;
        SwfFileCache.LoadAsync(finalPath);
        return null;
    }

    public Shader LoadShader(string? vs, string? fs)
    {
        return ShaderCache.Load(vs, fs);
    }

    public Texture2DWrapper? LoadShapeFromSwf(string filePath, ushort shapeId, double animScale)
    {
        SwfFileData? swf = LoadSwf(filePath);
        if (swf is null)
            return null;
        SwfShapeCache.Cache.TryGetValue((swf, shapeId, animScale), out Texture2DWrapper? texture);
        if (texture is not null)
            return texture;
        SwfShapeCache.LoadAsync(swf, shapeId, animScale);
        return null;
    }

    public SwfSprite? LoadSpriteFromSwf(string filePath, ushort spriteId)
    {
        SwfFileData? swf = LoadSwf(filePath);
        if (swf is null)
            return null;
        SwfSpriteCache.Cache.TryGetValue((swf, spriteId), out SwfSprite? sprite);
        if (sprite is not null)
            return sprite;
        SwfSpriteCache.LoadAsync(swf, spriteId);
        return null;
    }

    public const int MAX_TEXTURE_UPLOADS_PER_FRAME = 5;
    public const int MAX_SWF_TEXTURE_UPLOADS_PER_FRAME = 5;
    public void FinalizeDraw()
    {
        TextureCache.Upload(MAX_TEXTURE_UPLOADS_PER_FRAME);
        SwfShapeCache.Upload(MAX_SWF_TEXTURE_UPLOADS_PER_FRAME);

        while (DrawingQueue.Count > 0)
        {
            (_, Action drawAction) = DrawingQueue.PopMin();
            drawAction();
        }
    }

    public void DrawAnim(Gfx gfx, string animName, int frame, Transform trans, DrawPriorityEnum priority, object? caller, int loopLimit = -1)
    {
        ColorTransform? colorTransform = gfx.Tint == 0 ? null : new()
        {
            RMult = (short)((gfx.Tint >> 16) & 0xFF),
            GMult = (short)((gfx.Tint >> 8) & 0xFF),
            BMult = (short)((gfx.Tint >> 0) & 0xFF),
        };
        ColorTransform[] colorTransfroms = colorTransform is not null ? [colorTransform] : [];
        /*
        NOTE: the game goes over the list from the end until it finds a CustomArt that matches
        this only matters for CustomArt with RIGHT and for AsymmetrySwapFlags.
        we don't need that yet so just take last.
        */
        CustomArt? customArt = gfx.CustomArts.Length == 0 ? null : gfx.CustomArts[^1];
        string customArtSuffix = customArt is not null ? $"_{customArt.Name}" : "";
        // swf animation
        if (gfx.AnimFile.StartsWith("SFX_"))
        {
            SwfFileData? swf = LoadSwf(gfx.AnimFile);
            if (swf is null)
                return;
            ushort spriteId = swf.SymbolClass[gfx.AnimClass + customArtSuffix];
            DrawSwfSprite(gfx.AnimFile, spriteId, frame, gfx.AnimScale, colorTransfroms, 1, trans, priority, caller, loopLimit);
        }
        // anm animation
        else if (gfx.AnimFile.StartsWith("Animation_"))
        {
            if (!AnmClasses.TryGetValue($"{gfx.AnimFile}/{gfx.AnimClass}", out AnmClass? anmClass))
                return;
            // anm animation
            AnmAnimation animation = anmClass.Animations[animName];

            if (loopLimit != -1 && Math.Abs(frame) >= loopLimit * animation.Frames.Count)
                return;

            AnmFrame anmFrame = animation.Frames[BrawlhallaMath.SafeMod(frame, animation.Frames.Count)];
            foreach (AnmBone bone in anmFrame.Bones)
            {
                Transform boneTrans = new(bone.ScaleX, bone.RotateSkew1, bone.RotateSkew0, bone.ScaleY, bone.X, bone.Y);
                string swfPath = Path.Combine("bones", $"Bones{anmClass.FileName["Animation".Length..]}");
                string spriteName = BoneNames[bone.Id - 1] + customArtSuffix; // bone id is 1 indexed
                // wtf
                if (spriteName == "flash.display::MovieClip")
                    return;
                SwfFileData? swf = LoadSwf(swfPath);
                if (swf is null)
                    return;
                ushort spriteId = swf.SymbolClass[spriteName];
                DrawSwfSprite(swfPath, spriteId, bone.Frame - 1, gfx.AnimScale, colorTransfroms, bone.Opacity, trans * boneTrans, priority, caller);
            }
        }
    }

    public void DrawSwfShape(string filePath, ushort shapeId, double animScale, ColorTransform[] colorTransforms, double opacity, Transform trans, DrawPriorityEnum priority, object? caller)
    {
        Texture2DWrapper? texture = LoadShapeFromSwf(filePath, shapeId, animScale);
        if (texture is null) return;
        Shader shader = LoadShader(null, ColorTransform.CreateShader(colorTransforms));
        DrawingQueue.Push((caller, () =>
        {
            DrawTextureWithTransform(texture.Texture, shader, 0, 0, texture.W, texture.H, trans * Transform.CreateTranslate(texture.XOff, texture.YOff), opacity);
        }
        ), (int)priority);
    }

    public void DrawSwfSprite(string filePath, ushort spriteId, int frame, double animScale, ColorTransform[] colorTransforms, double opacity, Transform trans, DrawPriorityEnum priority, object? caller, int loopLimit = -1)
    {
        SwfFileData? file = LoadSwf(filePath);
        if (file is null) return;
        SwfSprite? sprite = LoadSpriteFromSwf(filePath, spriteId);
        if (sprite is null) return;

        if (loopLimit != -1 && Math.Abs(frame) >= loopLimit * sprite.Frames.Length)
            return;

        SwfSpriteFrame spriteFrame = sprite.Frames[BrawlhallaMath.SafeMod(frame, sprite.Frames.Length)];
        foreach ((_, SwfSpriteFrameLayer layer) in spriteFrame.Layers)
        {
            ColorTransform[] newColorTransform = layer.ColorTransform is not null ? [.. colorTransforms, layer.ColorTransform] : colorTransforms;
            Transform newTrans = trans * Utils.SwfMatrixToTransform(layer.Matrix);
            // is a shape
            if (file.ShapeTags.TryGetValue(layer.CharacterId, out DefineShapeXTag? shape))
            {
                ushort shapeId = shape.ShapeID;
                DrawSwfShape(filePath, shapeId, animScale, newColorTransform, opacity, newTrans, priority, caller);
            }
            // is a sprite
            else if (file.SpriteTags.TryGetValue(layer.CharacterId, out DefineSpriteTag? childSprite))
            {
                ushort childSpriteId = childSprite.SpriteID;
                DrawSwfSprite(filePath, childSpriteId, frame + layer.FrameOffset, animScale, newColorTransform, opacity, newTrans, priority, caller);
            }
        }
    }
}