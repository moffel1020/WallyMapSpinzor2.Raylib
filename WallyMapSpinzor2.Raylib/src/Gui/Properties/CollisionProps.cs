using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace WallyMapSpinzor2.Raylib;

public partial class PropertiesWindow
{
    public static bool ShowCollisionProps(AbstractCollision ac, CommandHistory cmd, PropertiesWindowData data) => ac switch
    {
        AbstractPressurePlateCollision pc => ShowAbstractPressurePlateCollisionProps(pc, cmd, data),
        LavaCollision lc => ShowLavaCollisionProps(lc, cmd, data),
        _ => ShowAbstractCollisionProps(ac, cmd, data)
    };

    public static bool ShowAbstractCollisionProps(AbstractCollision ac, CommandHistory cmd, PropertiesWindowData data)
    {
        if (ac.Parent is not null)
        {
            ImGui.Text($"Parent DynamicCollision: ");
            ImGui.SameLine();
            if (ImGui.Button($"PlatID {ac.Parent.PlatID}")) data.Selection.Object = ac.Parent;
            ImGui.Separator();
        }

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
            (ac.X1 + ac.X2) / 2 + (ac.Parent?.X ?? 0), (ac.Y1 + ac.Y2) / 2 + (ac.Parent?.Y ?? 0),
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
        propChanged |= ShowAbstractCollisionProps(pc, cmd, data);
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
                ImGui.Text($"{pc.TrapPowers[index]}");
                if (ImGui.Button($"Edit##trappower{index}"))
                    ImGui.OpenPopup(POWER_POPUP_NAME + index);
                bool changed = PowerEditPopup(data.PowerNames, pc.TrapPowers[index], val => pc.TrapPowers[index] = val, cmd, index.ToString());
                ImGui.SameLine();
                return changed;
            }, cmd, allowMove: false);
        }

        return propChanged;
    }

    public static bool ShowLavaCollisionProps(LavaCollision lc, CommandHistory cmd, PropertiesWindowData data)
    {
        bool propChanged = ShowAbstractCollisionProps(lc, cmd, data);

        ImGui.SeparatorText($"Lava collision props##props{lc.GetHashCode()}");
        if (data.PowerNames is null)
        {
            ImGui.Text("In order to edit the LavaPower, import powerTypes.csv");
            ImGui.Spacing();
            ImGui.Text("LavaPower: " + lc.LavaPower);
        }
        else
        {
            ImGui.Text($"Power: {lc.LavaPower}");
            ImGui.SameLine();
            if (ImGui.Button("Edit##lavapower"))
                ImGui.OpenPopup(POWER_POPUP_NAME);
            propChanged |= PowerEditPopup(data.PowerNames, lc.LavaPower, val => lc.LavaPower = val, cmd);
        }

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

    private const string POWER_POPUP_NAME = "PowerEdit";
    private static string _powerFilter = "";

    private static bool PowerEditPopup(string[] allPowers, string currentPower, Action<string> change, CommandHistory cmd, string popupId = "")
    {
        bool propChanged = false;
        if (ImGui.BeginPopup(POWER_POPUP_NAME + popupId, ImGuiWindowFlags.NoMove))
        {
            _powerFilter = ImGuiExt.InputText("##powerfilter", _powerFilter, flags: ImGuiInputTextFlags.None);
            if (_powerFilter != "")
            {
                ImGui.SameLine();
                if (ImGui.Button("x")) _powerFilter = "";
            }
            ImGui.SameLine();
            ImGui.Text("Filter");
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered()) 
                ImGui.SetTooltip("Search through all powertypes in the game. Note that not all powers will be compatible with traps/lava and changing this can crash the game.");

            string[] powers = allPowers
                .Where(p => p.Contains(_powerFilter, StringComparison.InvariantCultureIgnoreCase))
                .ToArray();

            string newPower = ImGuiExt.StringListBox("Power", currentPower, powers, 320.0f);
            if (currentPower != newPower)
            {
                cmd.Add(new PropChangeCommand<string>(change, currentPower, newPower));
                propChanged = true;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        return propChanged;
    }
}