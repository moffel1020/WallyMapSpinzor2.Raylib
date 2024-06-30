using System;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using NativeFileDialogSharp;

using Raylib_cs;
using Rl = Raylib_cs.Raylib;
using ImGuiNET;

using SwfLib.Tags.ActionsTags;
using BrawlhallaSwz;
using AbcDisassembler;

namespace WallyMapSpinzor2.Raylib;

public class ImportDialog(Editor editor, PathPreferences prefs) : IDialog
{
    private const int MAX_KEY_LENGTH = 9;

    private string? savedLdPath = prefs.LevelDescPath;
    private string? savedLtPath = prefs.LevelTypePath;
    private string? savedLstPath = prefs.LevelSetTypesPath;
    private string? savedBtPath = prefs.BoneTypesPath;
    private string? savedPtPath = prefs.PowerTypesPath;

    private readonly Dictionary<string, string> levelDescFiles = [];
    private string _levelDescFileFilter = "";
    private string? _pickedFileName;
    private LevelTypes? _decryptedLt;
    private LevelSetTypes? _decryptedLst;
    private string[]? _boneNames;
    private string[]? _powerNames;

    private string? _loadingError;
    private string? _loadingStatus;

    private bool _decrypting = false;
    private bool _keySearching = false;

    private bool _open = true;
    public bool Closed => !_open;

    public void Show()
    {
        ImGui.SetNextWindowSizeConstraints(new(500, 410), new(int.MaxValue));
        ImGui.Begin("Import", ref _open, ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoCollapse);

        ImGui.BeginTabBar("importTabBar", ImGuiTabBarFlags.None);
        if (ImGui.BeginTabItem("Brawlhalla"))
        {
            ShowGameImportTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("xml"))
        {
            ShowXmlImportTab();
            ImGui.EndTabItem();
        }

        if (_loadingStatus is not null)
        {
            ImGui.PushTextWrapPos();
            ImGui.Text(_loadingStatus);
            ImGui.PopTextWrapPos();
        }

        if (_loadingError is not null)
        {
            ImGui.PushTextWrapPos();
            ImGui.Text("[Error]: " + _loadingError);
            ImGui.PopTextWrapPos();
        }

        ImGui.EndTabBar();
        ImGui.End();
    }

    private void ShowGameImportTab()
    {
        ImGui.Text("Import from game swz files");
        ImGui.Separator();
        if (ImGui.Button("Select Brawlhalla Path"))
        {
            Task.Run(() =>
            {
                DialogResult result = Dialog.FolderPicker(prefs.BrawlhallaPath);
                if (result.IsOk)
                    prefs.BrawlhallaPath = result.Path;
            });
        }
        ImGui.Text($"Path: {prefs.BrawlhallaPath}");

        prefs.DecryptionKey = ImGuiExt.InputText("Decryption key", prefs.DecryptionKey ?? "", MAX_KEY_LENGTH, ImGuiInputTextFlags.CharsDecimal);
        if (prefs.DecryptionKey.Length > 0 && _decryptedLt is null && ImGuiExt.WithDisabledButton(_decrypting, "Decrypt"))
        {
            _decrypting = true;
            _loadingStatus = "decrypting...";
            Task.Run(() =>
            {
                try
                {
                    DecryptSwzFiles(prefs.BrawlhallaPath!);
                    _loadingStatus = null;
                    _loadingError = null;
                }
                catch (Exception e)
                {
                    Rl.TraceLog(TraceLogLevel.Error, e.Message);
                    Rl.TraceLog(TraceLogLevel.Trace, e.StackTrace);
                    _loadingStatus = null;
                    _loadingError = $"Could not decrypt swz files. {e.Message}";
                }
                finally
                {
                    _decrypting = false;
                }
            });
        }

        if (levelDescFiles.Count > 0 && _decryptedLt is not null && _decryptedLst is not null && _boneNames is not null)
        {
            _levelDescFileFilter = ImGuiExt.InputText("Filter map names", _levelDescFileFilter);
            string[] levelDescs = levelDescFiles.Keys
                .Where(s => s.Contains(_levelDescFileFilter, StringComparison.InvariantCultureIgnoreCase))
                .ToArray();
            int pickedItem = Array.FindIndex(levelDescs, s => s == _pickedFileName);
            if (ImGui.ListBox("Pick level file", ref pickedItem, levelDescs, levelDescs.Length, 12))
            {
                _pickedFileName = levelDescs[pickedItem];
            }

            if (ImGuiExt.WithDisabledButton(_pickedFileName is null, "Import"))
            {
                //TODO: figure out how to make this async
                //the main problem is ContinueWith doesn't run in main thread
                _loadingStatus = "loading...";
                try
                {
                    LevelDesc ld = Utils.DeserializeFromString<LevelDesc>(levelDescFiles[_pickedFileName!]);
                    _loadingStatus = null;
                    _loadingError = null;
                    editor.LoadMapFromLevel(new Level(ld, _decryptedLt, _decryptedLst), _boneNames, _powerNames);
                }
                catch (Exception e)
                {
                    Rl.TraceLog(TraceLogLevel.Error, e.Message);
                    Rl.TraceLog(TraceLogLevel.Trace, e.StackTrace);
                    _loadingStatus = null;
                    _loadingError = $"Failed to load map file. {e.Message}";
                }
            }
        }

        ImGui.Separator();
        if (ImGui.Button("Select BrawlhallaAir.swf"))
        {
            Task.Run(() =>
            {
                DialogResult result = Dialog.FileOpen("swf", prefs.BrawlhallaPath);
                if (result.IsOk)
                {
                    prefs.BrawlhallaAirPath = result.Path;
                }
            });
        }
        ImGui.Text($"{prefs.BrawlhallaAirPath}");

        if (prefs.BrawlhallaAirPath is not null && ImGuiExt.WithDisabledButton(_keySearching, "Find decryption key"))
        {
            _keySearching = true;
            _loadingStatus = "searching...";
            Task.Run(() =>
            {
                try
                {
                    if (Utils.GetDoABCDefineTag(prefs.BrawlhallaAirPath) is DoABCDefineTag abcTag)
                    {
                        AbcFile abc = AbcFile.Read(abcTag.ABCData);
                        uint? key = Utils.FindDecryptionKey(abc);
                        if (key is not null)
                        {
                            prefs.DecryptionKey = key.ToString();
                        }

                        _loadingStatus = null;
                        _loadingError = null;
                    }
                    else
                    {
                        _loadingStatus = null;
                        _loadingError = "Could not find decryption key.";
                    }
                }
                catch (Exception e)
                {
                    Rl.TraceLog(TraceLogLevel.Error, e.Message);
                    Rl.TraceLog(TraceLogLevel.Trace, e.StackTrace);
                    _loadingStatus = null;
                    _loadingError = "Could not find decryption key. " + e.Message;
                }
                finally
                {
                    _keySearching = false;
                }
            });
        }
    }

    private void ShowXmlImportTab()
    {
        ImGui.PushTextWrapPos();
        ImGui.Text("Import from LevelDesc xml file, LevelTypes.xml, and LevelSetTypes.xml");
        ImGui.Text("If LevelTypes.xml is not selected or it does not contain the level a default LevelType will be generated");
        ImGui.PopTextWrapPos();
        ImGui.SeparatorText("Select files");

        if (ImGui.Button("LevelDesc"))
        {
            Task.Run(() =>
            {
                DialogResult result = Dialog.FileOpen("xml", Path.GetDirectoryName(savedLdPath));
                if (result.IsOk)
                    savedLdPath = result.Path;
            });
        }
        ImGui.SameLine();
        ImGui.Text(savedLdPath ?? "None");

        if (ImGui.Button("BoneTypes.xml"))
        {
            Task.Run(() =>
            {
                DialogResult result = Dialog.FileOpen("xml", Path.GetDirectoryName(savedBtPath));
                if (result.IsOk)
                    savedBtPath = result.Path;
            });
        }
        ImGui.SameLine();
        ImGui.Text(savedBtPath ?? "None");

        if (ImGui.Button("powerTypes.csv (optional)"))
        {
            Task.Run(() =>
            {
                DialogResult result = Dialog.FileOpen("csv", Path.GetDirectoryName(savedPtPath));
                if (result.IsOk)
                    savedPtPath = result.Path;
            });
        }
        ImGui.SameLine();
        if (savedPtPath is not null && ImGui.Button("x##pt")) savedPtPath = null;
        ImGui.SameLine();
        ImGui.Text(savedPtPath ?? "None");

        if (ImGui.Button("LevelTypes.xml (optional)"))
        {
            Task.Run(() =>
            {
                DialogResult result = Dialog.FileOpen("xml", Path.GetDirectoryName(savedLtPath));
                if (result.IsOk)
                    savedLtPath = result.Path;
            });
        }
        ImGui.SameLine();
        if (savedLtPath is not null && ImGui.Button("x##lt")) savedLtPath = null;
        ImGui.SameLine();
        ImGui.Text(savedLtPath ?? "None");

        if (ImGui.Button("LevelSetTypes.xml (optional)"))
        {
            Task.Run(() =>
            {
                DialogResult result = Dialog.FileOpen("xml", Path.GetDirectoryName(savedLstPath));
                if (result.IsOk)
                    savedLstPath = result.Path;
            });
        }
        ImGui.SameLine();
        if (savedLstPath is not null && ImGui.Button("x##lst")) savedLstPath = null;
        ImGui.SameLine();
        ImGui.Text(savedLstPath ?? "None");

        ImGui.Separator();
        if (savedLdPath is not null && (editor.BoneNames is not null || savedBtPath is not null) && ImGui.Button("Import"))
        {
            _loadingStatus = "loading...";
            try
            {
                editor.LoadMapFromPaths(savedLdPath, savedLtPath, savedLstPath, savedBtPath, savedPtPath);
                _open = false;
                _loadingStatus = null;
                _loadingError = null;
                prefs.LevelDescPath = savedLdPath;
                prefs.LevelTypePath = savedLtPath;
                prefs.LevelSetTypesPath = savedLstPath;
                prefs.BoneTypesPath = savedBtPath;
                prefs.PowerTypesPath = savedPtPath;
            }
            catch (Exception e)
            {
                Rl.TraceLog(TraceLogLevel.Error, e.Message);
                Rl.TraceLog(TraceLogLevel.Trace, e.StackTrace);
                _loadingStatus = null;
                _loadingError = $"Could not load xml file. {e.Message}";
            }
        }
    }

    private void DecryptSwzFiles(string folder)
    {
        string gamePath = Path.Combine(folder, "Game.swz");
        string dynamicPath = Path.Combine(folder, "Dynamic.swz");
        string initPath = Path.Combine(folder, "Init.swz");
        uint key = uint.Parse(prefs.DecryptionKey!);

        levelDescFiles.Clear();
        foreach (string file in Utils.GetFilesInSwz(dynamicPath, key))
        {
            string name = SwzUtils.GetFileName(file);
            if (!name.StartsWith("LevelDesc_"))
                continue;
            levelDescFiles.Add(name["LevelDesc_".Length..], file);
        }

        // TODO: avoid read-decrypting files multiple times
        _decryptedLt = Utils.DeserializeSwzFromPath<LevelTypes>(initPath, "LevelTypes.xml", key);
        _decryptedLst = Utils.DeserializeSwzFromPath<LevelSetTypes>(gamePath, "LevelSetTypes.xml", key);
        string? boneTypesContent = Utils.GetFileInSwzFromPath(initPath, "BoneTypes.xml", key);
        string? powerTypesContent = Utils.GetFileInSwzFromPath(gamePath, "powerTypes.csv", key);

        if (boneTypesContent is null)
            _boneNames = null;
        else
        {
            _boneNames = [.. XElement.Parse(boneTypesContent).Elements("Bone").Select(e => e.Value)];
        }

        if (powerTypesContent is not null)
            _powerNames = Utils.ParsePowerTypes(powerTypesContent);
    }
}