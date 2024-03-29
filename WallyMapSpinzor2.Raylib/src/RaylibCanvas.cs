using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using Rl = Raylib_cs.Raylib;
using Raylib_cs;

namespace WallyMapSpinzor2.Raylib;

public class RaylibCanvas(string brawlPath) : ICanvas<Texture2DWrapper>
{
    public BucketPriorityQueue<Action> DrawingQueue { get; } = new(Enum.GetValues<DrawPriorityEnum>().Length);
    public TextureCache TextureCache { get; } = new();
    public SwfFileCache SwfFileCache { get; } = new();
    public SwfTextureCache SwfTextureCache { get; } = new();
    public Matrix4x4 CameraMatrix { get; set; } = Matrix4x4.Identity;

    public void ClearTextureCache()
    {
        TextureCache.Clear();
    }

    public void DrawCircle(double x, double y, double radius, Color color, Transform trans, DrawPriorityEnum priority)
    {
        // FIXME: doesn't account for transformations affecting radius (could be turned into an ellipse)
        (x, y) = trans * (x, y);

        DrawingQueue.Push(() =>
        {
            Rl.DrawCircle((int)x, (int)y, (float)radius, Utils.ToRlColor(color));
        }, (int)priority);
    }

    public void DrawLine(double x1, double y1, double x2, double y2, Color color, Transform trans, DrawPriorityEnum priority)
    {
        (x1, y1) = trans * (x1, y1);
        (x2, y2) = trans * (x2, y2);

        DrawingQueue.Push(() =>
        {
            Rl.DrawLine((int)x1, (int)y1, (int)x2, (int)y2, Utils.ToRlColor(color));
        }, (int)priority);
    }

    public const double MULTI_COLOR_LINE_OFFSET = 5;
    public void DrawLineMultiColor(double x1, double y1, double x2, double y2, Color[] colors, Transform trans, DrawPriorityEnum priority)
    {
        (x1, y1) = trans * (x1, y1);
        (x2, y2) = trans * (x2, y2);
        if (x1 > x2)
        {
            (x1, x2) = (x2, x1);
            (y1, y2) = (y2, y1);
        }
        double center = (colors.Length - 1) / 2.0;
        (double offX, double offY) = (y1 - y2, x2 - x1);
        (offX, offY) = BrawlhallaMath.Normalize(offX, offY);
        for (int i = 0; i < colors.Length; ++i)
        {
            double mult = MULTI_COLOR_LINE_OFFSET * (i - center);
            DrawLine(x1 + offX * mult, y1 + offY * mult, x2 + offX * mult, y2 + offY * mult, colors[i], Transform.IDENTITY, priority);
        }
        // version that should be camera-independent. doesn't work properly.
        // line offset ends up being really big when far away
        /*
        Debug.Assert(Matrix4x4.Invert(CameraMatrix, out Matrix4x4 invertedMat));
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
        (double offX, double offY) = (y1 - y2, x2 - x1);
        (offX, offY) = BrawlhallaMath.Normalize(offX, offY);
        for (int i = 0; i < colors.Length; ++i)
        {
            double mult = MULTI_COLOR_LINE_OFFSET * (i - center);
            DrawLine(x1 + offX * mult, y1 + offY * mult, x2 + offX * mult, y2 + offY * mult, colors[i], inv, priority);
        }
        */
    }

    public void DrawRect(double x, double y, double w, double h, bool filled, Color color, Transform trans, DrawPriorityEnum priority)
    {
        DrawingQueue.Push(() =>
        {
            if (filled)
            {
                DrawRectWithTransform(x, y, w, h, trans, color);
            }
            else
            {
                DrawLine(x, y, x + w, y, color, trans, priority);
                DrawLine(x + w, y, x + w, y + h, color, trans, priority);
                DrawLine(x + w, y + h, x, y + h, color, trans, priority);
                DrawLine(x, y + h, x, y, color, trans, priority);
            }
        }, (int)priority);
    }

    public void DrawString(double x, double y, string text, double fontSize, Color color, Transform trans, DrawPriorityEnum priority)
    {

    }

    public void DrawTexture(double x, double y, Texture2DWrapper texture, Transform trans, DrawPriorityEnum priority)
    {
        DrawingQueue.Push(() =>
        {
            DrawTextureWithTransform(texture.Texture, x + texture.XOff, y + texture.YOff, texture.W, texture.H, trans, Color.FromHex(0xFFFFFFFF));
        }, (int)priority);
    }

    public void DrawTextureRect(double x, double y, double w, double h, Texture2DWrapper texture, Transform trans, DrawPriorityEnum priority)
    {
        DrawingQueue.Push(() =>
        {
            DrawTextureWithTransform(texture.Texture, x + texture.XOff, y + texture.YOff, w, h, trans, Color.FromHex(0xFFFFFFFF));
        }, (int)priority);
    }

    private static void DrawTextureWithTransform(Texture2D texture, double x, double y, double w, double h, Transform trans, Color color)
    {
        Rlgl.SetTexture(texture.Id);
        Rlgl.Begin(DrawMode.Quads);
        Rlgl.Color4ub(color.R, color.G, color.B, color.A);
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

        _ = TextureCache.LoadImageAsync(finalPath);
        return Texture2DWrapper.Default; // placeholder white texture until the image is read from disk
    }

    public Texture2DWrapper LoadTextureFromSWF(string filePath, string name)
    {
        string finalPath = Path.Combine(brawlPath, filePath);
        SwfFileCache.Cache.TryGetValue(finalPath, out SwfFileData? swf);
        if (swf is not null)
        {
            SwfTextureCache.Cache.TryGetValue((swf, name), out Texture2DWrapper? texture);
            if (texture is not null)
            {
                return texture;
            }

            _ = SwfTextureCache.LoadImageAsync(swf, name);
            return Texture2DWrapper.Default;
        }

        _ = SwfFileCache.LoadSwfAsync(finalPath);
        return Texture2DWrapper.Default;
    }

    public const int MAX_TEXTURE_UPLOADS_PER_FRAME = 5;
    public const int MAX_SWF_UPLOADS_PER_FRAME = 1;
    public const int MAX_SWF_TEXTURE_UPLOADS_PER_FRAME = 5;
    public void FinalizeDraw()
    {
        TextureCache.UploadImages(MAX_TEXTURE_UPLOADS_PER_FRAME);
        SwfFileCache.UploadSwfs(MAX_SWF_UPLOADS_PER_FRAME);
        SwfTextureCache.UploadImages(MAX_SWF_TEXTURE_UPLOADS_PER_FRAME);

        while (DrawingQueue.Count > 0)
        {
            Action drawAction = DrawingQueue.PopMin();
            drawAction();
        }
    }
}