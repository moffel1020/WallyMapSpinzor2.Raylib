using ImGuiNET;

namespace WallyMapSpinzor2.Raylib;

public partial class PropertiesWindow
{
    public static bool ShowMovingPlatformProps(MovingPlatform mp, CommandHistory cmd, PropertiesWindowData data)
    {
        bool propChanged = false;
        ImGui.Text("PlatID: " + mp.PlatID);
        propChanged |= ImGuiExt.DragFloatHistory("X##mp", mp.X, val => mp.X = val, cmd);
        propChanged |= ImGuiExt.DragFloatHistory("Y##mp", mp.Y, val => mp.Y = val, cmd);
        if (ImGui.CollapsingHeader("Animation"))
            propChanged |= ShowAnimationProps(mp.Animation, cmd);
        ImGui.Separator();
        if (mp.AssetName is null && ImGui.CollapsingHeader("Children"))
        {
            propChanged |= ImGuiExt.EditArrayHistory("", mp.Assets, val => mp.Assets = val,
            () => CreateNewMovingPlatformChild(mp),
            (int index) =>
            {
                bool changed = false;
                AbstractAsset child = mp.Assets[index];
                if (ImGui.TreeNode($"{child.GetType().Name} {MapOverviewWindow.GetExtraObjectInfo(child)}##{child.GetHashCode()}"))
                {
                    changed |= ShowProperties(child, cmd, data);
                    ImGui.TreePop();
                }

                if (ImGui.Button($"Select##mpchild{index}")) data.Selection.Object = child;
                ImGui.SameLine();

                return changed;
            }, cmd);
        }
        return propChanged;
    }

    private static Maybe<AbstractAsset> CreateNewMovingPlatformChild(MovingPlatform parent)
    {
        Maybe<AbstractAsset> result = new();
        if (ImGui.Button("Add new child"))
            ImGui.OpenPopup("AddChild##moving_platform");

        if (ImGui.BeginPopup("AddChild##moving_platform"))
        {
            result = AddObjectPopup.AddAssetMenu(new(0, 0));
            result.DoIfSome(a => a.Parent = parent);
            ImGui.EndPopup();
        }
        return result;
    }
}