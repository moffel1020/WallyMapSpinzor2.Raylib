using System;
using System.Linq;
using ImGuiNET;

namespace WallyMapSpinzor2.Raylib;

public partial class PropertiesWindow
{
    public static bool ShowNavNodeProps(NavNode n, CommandHistory cmd)
    {
        bool propChanged = false;
        ImGui.Text("NavID: " + n.NavID);
        propChanged |= ImGuiExt.GenericStringComboHistory("NavType", n.Type, val => n.Type = val,
        t => t switch
        {
            not NavNodeTypeEnum._ => t.ToString(),
            _ => "None",
        },
        t => t switch
        {
            "None" => NavNodeTypeEnum._,
            _ => Enum.Parse<NavNodeTypeEnum>(t),
        }, [.. Enum.GetValues<NavNodeTypeEnum>().Where(t => t != NavNodeTypeEnum.D)], cmd);

        ImGui.TextWrapped("Path: " + string.Join(", ", n.Path.Select(nn => nn.Item1)));
        propChanged |= ImGuiExt.DragFloatHistory("X", n.X, val => n.X = val, cmd);
        propChanged |= ImGuiExt.DragFloatHistory("Y", n.Y, val => n.Y = val, cmd);
        return propChanged;
    }
}