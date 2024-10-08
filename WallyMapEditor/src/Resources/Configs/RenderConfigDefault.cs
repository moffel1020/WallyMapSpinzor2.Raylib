using System;
using System.IO;
using System.Xml.Linq;
using Raylib_cs;
using WallyMapSpinzor2;

namespace WallyMapEditor;

public class RenderConfigDefault : IDeserializable, ISerializable
{
    public const string APPDATA_DIR_NAME = "WallyMapEditor";
    public const string FILE_NAME = "RenderConfigDefault.xml";

    public RenderConfig ConfigDefault { get; set; } = RenderConfig.Default;

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        APPDATA_DIR_NAME,
        FILE_NAME
    );


    public static RenderConfigDefault Load()
    {
        string? dir = Path.GetDirectoryName(FilePath);
        if (dir is not null)
        {
            Directory.CreateDirectory(dir);
            if (File.Exists(FilePath))
            {
                return WmeUtils.DeserializeFromPath<RenderConfigDefault>(FilePath);
            }
        }

        return new();
    }

    public void Save()
    {
        WmeUtils.SerializeToPath(this, FilePath);
    }

    public void Deserialize(XElement e)
    {
        ConfigDefault.Deserialize(e);
    }

    public void Serialize(XElement e)
    {
        ConfigDefault.Serialize(e);
    }

    public void ApplyCmdlineOverrides(CommandLineArgs args)
    {
        if (args.TryGetArg("--defaultConfig", out string? defaultConfig))
        {
            if (!File.Exists(defaultConfig))
                Rl.TraceLog(TraceLogLevel.Warning, "Default config path does not exist");
            else
                try
                {
                    ConfigDefault = WmeUtils.DeserializeFromPath<RenderConfigDefault>(defaultConfig).ConfigDefault;
                }
                catch
                {
                    Rl.TraceLog(TraceLogLevel.Warning, "Default config path leads to an invalid file");
                }
        }
    }
}