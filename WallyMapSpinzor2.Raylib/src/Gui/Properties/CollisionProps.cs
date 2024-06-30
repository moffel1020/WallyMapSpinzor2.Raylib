using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;

namespace WallyMapSpinzor2.Raylib;

public partial class PropertiesWindow
{
    public static bool ShowCollisionProps(AbstractCollision ac, CommandHistory cmd, PropertiesWindowData data) => ac switch
    {
        AbstractPressurePlateCollision pc => ShowAbstractPressurePlateCollisionProps(pc, cmd, data),
        LavaCollision lc => ShowLavaCollisionProps(lc, cmd),
        _ => ShowAbstractCollisionProps(ac, cmd)
    };

    public static bool ShowAbstractCollisionProps(AbstractCollision ac, CommandHistory cmd)
    {
        bool propChanged = false;
        propChanged |= ImGuiExt.DragFloatHistory($"X1##props{ac.GetHashCode()}", ac.X1, val => ac.X1 = val, cmd);
        propChanged |= ImGuiExt.DragFloatHistory($"Y1##props{ac.GetHashCode()}", ac.Y1, val => ac.Y1 = val, cmd);
        propChanged |= ImGuiExt.DragFloatHistory($"X2##props{ac.GetHashCode()}", ac.X2, val => ac.X2 = val, cmd);
        propChanged |= ImGuiExt.DragFloatHistory($"Y2##props{ac.GetHashCode()}", ac.Y2, val => ac.Y2 = val, cmd);
        propChanged |= ImGuiExt.GenericStringComboHistory($"Team##props{ac.GetHashCode()}", ac.Team, val => ac.Team = val,
        t => t switch
        {
            0 => "None",
            _ => t.ToString(),
        },
        t => t switch
        {
            "None" => 0,
            _ => int.Parse(t),
        }, [0, 1, 2, 3, 4, 5], cmd);
        propChanged |= ImGuiExt.NullableEnumComboHistory($"Flag##{ac.GetHashCode()}", ac.Flag, val => ac.Flag = val, cmd);
        propChanged |= ImGuiExt.NullableEnumComboHistory($"ColorFlag##{ac.GetHashCode()}", ac.ColorFlag, val => ac.ColorFlag = val, cmd);

        string tauntEventString = ac.TauntEvent ?? "";
        string newTauntEventString = ImGuiExt.InputText($"TauntEvent##{ac.GetHashCode()}", tauntEventString);
        if (tauntEventString != newTauntEventString)
        {
            cmd.Add(new PropChangeCommand<string?>(val => ac.TauntEvent = val, ac.TauntEvent, newTauntEventString == "" ? null : newTauntEventString));
            propChanged = true;
        }

        ImGui.SeparatorText($"Anchor##props{ac.GetHashCode()}");
        propChanged |= ImGuiExt.DragNullableFloatPairHistory(
            "anchor",
            $"AnchorX##props{ac.GetHashCode()}", $"AnchorY##props{ac.GetHashCode()}",
            ac.AnchorX, ac.AnchorY,
            // these default values look weird when the collision is on a moving platform
            // but the user will adjust it anyways so it's ok
            (ac.X1 + ac.X2) / 2, (ac.Y1 + ac.Y2) / 2,
            (val1, val2) => (ac.AnchorX, ac.AnchorY) = (val1, val2),
            cmd
        );

        ImGui.SeparatorText($"Normal##props{ac.GetHashCode()}");
        propChanged |= ImGuiExt.DragFloatHistory($"NormalX##props{ac.GetHashCode()}", ac.NormalX, val => ac.NormalX = val, cmd, speed: 0.01, minValue: -1, maxValue: 1);
        propChanged |= ImGuiExt.DragFloatHistory($"NormalY##props{ac.GetHashCode()}", ac.NormalY, val => ac.NormalY = val, cmd, speed: 0.01, minValue: -1, maxValue: 1);

        return propChanged;
    }

    public static bool ShowAbstractPressurePlateCollisionProps(AbstractPressurePlateCollision pc, CommandHistory cmd, PropertiesWindowData data)
    {
        bool propChanged = false;
        propChanged |= ShowAbstractCollisionProps(pc, cmd);
        ImGui.SeparatorText($"Pressure plate props##props{pc.GetHashCode()}");
        ImGui.Text("AssetName: " + pc.AssetName);
        if (data.Canvas is not null)
        {
            ImGuiExt.Animation(data.Canvas, pc.Gfx, "Ready", 0);
        }
        propChanged |= ImGuiExt.DragFloatHistory($"AnimOffseyX##props{pc.GetHashCode()}", pc.AnimOffsetX, val => pc.AnimOffsetX = val, cmd);
        propChanged |= ImGuiExt.DragFloatHistory($"AnimOffsetY##props{pc.GetHashCode()}", pc.AnimOffsetY, val => pc.AnimOffsetY = val, cmd);
        propChanged |= ImGuiExt.DragFloatHistory($"AnimRotation##props{pc.GetHashCode()}", pc.AnimRotation, val => pc.AnimRotation = BrawlhallaMath.SafeMod(val, 360.0), cmd);
        propChanged |= ImGuiExt.DragIntHistory($"Cooldown##props{pc.GetHashCode()}", pc.Cooldown, val => pc.Cooldown = val, cmd, minValue: 0);
        propChanged |= ImGuiExt.CheckboxHistory($"FaceLeft##props{pc.GetHashCode()}", pc.FaceLeft, val => pc.FaceLeft = val, cmd);
        //TODO: add FireOffsetX, FireOffsetY

        //TODO: allow modifying
        if (data.PowerNames is null)
        {
            ImGui.Text("In order to edit the TrapPowers, import powerTypes.csv");
            ImGui.Spacing();
            ImGui.Text("TrapPowers:");
            foreach (string power in pc.TrapPowers)
                ImGui.BulletText(power);
        }
        else
        {
            propChanged |= ImGuiExt.EditArrayHistory("TrapPowers", pc.TrapPowers, val => pc.TrapPowers = val,
            () =>
            {
                Maybe<string> result = new();
                if (ImGui.Button("Add new power"))
                    result = data.PowerNames[0];
                return result;
            },
            (int index) =>
            {
                bool changed = false;
                string power = pc.TrapPowers[index];
                string newPower = ImGuiExt.StringCombo($"##trappower{index}", power, data.PowerNames);
                if (power != newPower)
                {
                    cmd.Add(new PropChangeCommand<string>(val => pc.TrapPowers[index] = val, power, newPower));
                    changed = true;
                }
                return changed;
            }, cmd, allowMove: false);
        }

        return propChanged;
    }

    public static bool ShowLavaCollisionProps(LavaCollision lc, CommandHistory cmd)
    {
        bool propChanged = ShowAbstractCollisionProps(lc, cmd);

        ImGui.SeparatorText($"Lava collision props##props{lc.GetHashCode()}");
        ImGui.Text("LavaPower: " + lc.LavaPower); //TODO: allow modifying

        return propChanged;
    }

    public static C DefaultCollision<C>(Vector2 pos) where C : AbstractCollision, new()
    {
        C col = new() { X1 = pos.X, X2 = pos.X + 100, Y1 = pos.Y, Y2 = pos.Y };
        if (col is AbstractPressurePlateCollision pcol)
        {
            pcol.AssetName = "a__AnimationPressurePlate";
            pcol.FireOffsetX = [];
            pcol.FireOffsetY = [];
            pcol.TrapPowers = [];
            pcol.AnimOffsetX = (col.X1 + col.X2) / 2;
            pcol.AnimOffsetY = (col.Y1 + col.Y2) / 2;
            pcol.Cooldown = 3000;
        }
        if (col is LavaCollision lcol)
        {
            lcol.LavaPower = "LavaBurn";
        }
        return col;
    }
}