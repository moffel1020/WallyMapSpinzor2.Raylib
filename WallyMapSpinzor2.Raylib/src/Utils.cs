using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Numerics;
using System.Xml;
using System.Xml.Linq;
using System.Text;

using Rl = Raylib_cs.Raylib;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using SwfLib;
using SwfLib.Tags;
using SwfLib.Tags.ActionsTags;
using SwfLib.Data;

using BrawlhallaSwz;

using AbcDisassembler;
using nietras.SeparatedValues;

namespace WallyMapSpinzor2.Raylib;

public static class Utils
{
    public static readonly XmlWriterSettings StandardSaveSettings = new()
    {
        OmitXmlDeclaration = true, // no xml header
        IndentChars = "    ",
        Indent = true, // indent with four spaces
        NewLineChars = "\n", // use UNIX line endings
        Encoding = new UTF8Encoding(false) // use UTF8 (no BOM) encoding
    };

    public static readonly XmlWriterSettings MinifiedSaveSettings = new()
    {
        OmitXmlDeclaration = true, //no xml header
        NewLineChars = "", // do not newline
        Encoding = new UTF8Encoding(false) // use UTF8 (no BOM) encoding
    };

    //m11, m12, m13, m14
    //m21, m22, m23, m24
    //m31, m32, m33, m34
    //m41, m42, m43, m44
    public static Matrix4x4 TransformToMatrix4x4(Transform t) => new(
        (float)t.ScaleX, (float)t.SkewY, 0, 0,
        (float)t.SkewX, (float)t.ScaleY, 0, 0,
        0, 0, 1, 0,
        (float)t.TranslateX, (float)t.TranslateY, 0, 1
    );
    public static Transform Matrix4x4ToTransform(Matrix4x4 m) => new(m.M11, m.M21, m.M12, m.M22, m.M41, m.M42);
    public static Transform SwfMatrixToTransform(SwfMatrix m) => new(m.ScaleX, m.RotateSkew1, m.RotateSkew0, m.ScaleY, m.TranslateX / 20.0, m.TranslateY / 20.0);

    public static bool IsInDirectory(string dirPath, string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);
        while (dir is not null)
        {
            if (dir == dirPath)
                return true;
            dir = Path.GetDirectoryName(dir);
        }
        return false;
    }

    public static Raylib_cs.Color WmsColorToRlColor(Color c) => new(c.R, c.G, c.B, c.A);

    public static Raylib_cs.Image ImgSharpImageToRlImage(Image<Rgba32> image)
    {
        Raylib_cs.Image img;
        unsafe
        {
            int bufferSize = image.Width * image.Height * image.PixelType.BitsPerPixel / 8;
            // use Rl alloc so GC doesn't free the memory
            void* bufferPtr = Rl.MemAlloc(bufferSize);
            // create span so ImageSharp can write to the memory
            Span<byte> buffer = new(bufferPtr, bufferSize);
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

    public static Raylib_cs.Image LoadRlImage(string path)
    {
        Raylib_cs.Image img;
        if (path.EndsWith(".jpg"))
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(path);
            img = ImgSharpImageToRlImage(image);
        }
        else
        {
            img = Rl.LoadImage(path);
        }
        return img;
    }

    public static bool IsPolygonClockwise(IReadOnlyList<(double, double)> poly)
    {
        double area = 0;
        for (int i = 0; i < poly.Count; ++i)
        {
            int j = (i + 1) % poly.Count;
            (double x1, double y1) = poly[i];
            (double x2, double y2) = poly[j];
            area += BrawlhallaMath.Cross(x1, y1, x2, y2);
        }
        return area > 0;
    }

    public static T DeserializeFromPath<T>(string fromPath)
        where T : IDeserializable, new()
    {
        XElement element;
        using (FileStream fromFile = new(fromPath, FileMode.Open, FileAccess.Read))
        {
            // bmg moment
            if (fromPath.EndsWith("RefineryDoors.xml"))
            {
                string content;
                using (StreamReader reader = new(fromFile))
                    content = reader.ReadToEnd();
                content = content.Replace("--->", "-->");
                element = XElement.Parse(content);
            }
            else
            {
                element = XElement.Load(fromFile);
            }
        }
        return element.DeserializeTo<T>();
    }

    public static void SerializeToPath<T>(T serializable, string toPath, bool minify = false)
        where T : ISerializable
    {
        XElement e = serializable.SerializeToXElement();
        using FileStream toFile = new(toPath, FileMode.Create, FileAccess.Write);
        using XmlWriter xmlw = XmlWriter.Create(toFile, minify ? StandardSaveSettings : MinifiedSaveSettings);
        e.Save(xmlw);
    }

    public static string SerializeToString<T>(T serializable, bool minify = false)
        where T : ISerializable
    {
        XElement e = serializable.SerializeToXElement();
        using StringWriter sw = new();
        using XmlWriter xmlw = XmlWriter.Create(sw, minify ? StandardSaveSettings : MinifiedSaveSettings);
        e.Save(xmlw);
        xmlw.Flush();
        return sw.ToString();
    }

    public static T DeserializeFromString<T>(string xmldata)
        where T : IDeserializable, new()
    {
        // bmg moment
        xmldata = xmldata.Replace("--->", "-->");
        XElement element = XElement.Parse(xmldata);
        return element.DeserializeTo<T>();
    }

    public static IEnumerable<string> GetFilesInSwz(string swzPath, uint key)
    {
        using FileStream stream = new(swzPath, FileMode.Open, FileAccess.Read);
        using SwzReader reader = new(stream, key);
        while (reader.HasNext())
            yield return reader.ReadFile();
    }

    public static string? GetFileInSwzFromPath(string swzPath, string filename, uint key) =>
        GetFilesInSwz(swzPath, key).FirstOrDefault(file => SwzUtils.GetFileName(file) == filename);

    public static T? DeserializeSwzFromPath<T>(string swzPath, string filename, uint key)
        where T : IDeserializable, new()
    {
        string? content = GetFileInSwzFromPath(swzPath, filename, key);
        if (content is null) return default;
        return DeserializeFromString<T>(content);
    }

    public static void SerializeSwzFilesToPath(string swzPath, IEnumerable<string> swzFiles, uint key)
    {
        using FileStream stream = new(swzPath, FileMode.Create, FileAccess.Write);
        using SwzWriter writer = new(stream, key);
        foreach (string file in swzFiles)
            writer.WriteFile(file);
        writer.Flush();
    }

    private static List<int> FindGetlexPositions(CPoolInfo cpool, string lexName, List<Instruction> code) => code
        .Select((o, i) => new { Item = o, Index = i })
        .Where(o => o.Item.Name == "getlex" && o.Item.Args[0].Value is INamedMultiname name && cpool.Strings[(int)name.Name] == lexName)
        .Select(o => o.Index)
        .ToList();

    private static int FindCallpropvoidPos(CPoolInfo cpool, string methodName, List<Instruction> code) => code
        .FindIndex(i => i.Name == "callpropvoid" && i.Args[0].Value is INamedMultiname named && cpool.Strings[(int)named.Name] == methodName);

    private static uint? FindLastPushuintArg(List<Instruction> ins) => (uint?)ins
        .LastOrDefault(ins => ins.Name == "pushuint")?.Args[0].Value;

    public static uint? FindDecryptionKey(AbcFile abc)
    {
        foreach (MethodBodyInfo mb in abc.MethodBodies)
        {
            List<int> getlexPos = FindGetlexPositions(abc.ConstantPool, "ANE_RawData", mb.Code);

            for (int i = 0; i < getlexPos.Count; i++)
            {
                int callpropvoidPos = getlexPos[i] == getlexPos[^1]
                    ? FindCallpropvoidPos(abc.ConstantPool, "Init", mb.Code[getlexPos[i]..])
                    : FindCallpropvoidPos(abc.ConstantPool, "Init", mb.Code[getlexPos[i]..getlexPos[i + 1]]);

                if (callpropvoidPos != -1)
                    return FindLastPushuintArg(mb.Code[0..callpropvoidPos]);
            }
        }

        return null;
    }

    public static DoABCDefineTag? GetDoABCDefineTag(string swfPath)
    {
        SwfFile? swf;
        using (FileStream stream = new(swfPath, FileMode.Open, FileAccess.Read))
            swf = SwfFile.ReadFrom(stream);

        if (swf is not null)
        {
            foreach (SwfTagBase tag in swf.Tags)
            {
                if (tag is DoABCDefineTag abcTag)
                    return abcTag;
            }
        }

        return null;
    }

    public static bool IsValidBrawlPath(string? path)
    {
        if (path is null) return false;

        string[] requiredFiles = ["BrawlhallaAir.swf", "Dynamic.swz", "Game.swz", "Engine.swz"];
        foreach (string name in requiredFiles)
        {
            if (!File.Exists(Path.Combine(path, name)))
            {
                return false;
            }
        }

        return true;
    }

    public static string ChangePathName(string path, Func<string, string> modifier)
    {
        string ext = Path.GetExtension(path);
        string name = Path.GetFileNameWithoutExtension(path);
        string dir = Path.GetDirectoryName(path) ?? "";
        return Path.Combine(dir, Path.ChangeExtension(modifier(name), ext));
    }

    public static void CreateBackupOfFile(string path)
    {
        int suffix = 1;
        string backupPath;
        do
        {
            backupPath = ChangePathName(path, s => $"{s}_Backup{suffix}");
            suffix++;
        } while (File.Exists(backupPath));
        using FileStream read = new(path, FileMode.Open, FileAccess.Read);
        using FileStream write = new(backupPath, FileMode.CreateNew, FileAccess.Write);
        read.CopyTo(write);
    }

    public static T[] RemoveAt<T>(T[] array, int index)
    {
        T[] result = new T[array.Length - 1];
        for ((int i, int j) = (0, 0); i < array.Length; ++i)
        {
            if (i != index)
            {
                result[j] = array[i];
                ++j;
            }
        }
        return result;
    }

    public static T[] MoveUp<T>(T[] array, int index)
    {
        T[] result = [.. array];
        if (index > 0)
            (result[index], result[index - 1]) = (result[index - 1], result[index]);
        return result;
    }

    public static T[] MoveDown<T>(T[] array, int index)
    {
        T[] result = [.. array];
        if (index < array.Length - 1)
            (result[index], result[index + 1]) = (result[index + 1], result[index]);
        return result;
    }

    public static string[]? ParsePowerTypes(string str)
    {
        int lineEnd = str.IndexOf('\n');
        str = str[(lineEnd + 1)..];
        using SepReader reader = Sep.New(',').Reader().FromText(str);
        return [.. reader.Enumerate(row => row[0].ToString())];
    }
}
