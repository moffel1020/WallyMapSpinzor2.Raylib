namespace WallyMapSpinzor2.Raylib;

public partial class PropertiesWindow
{
    public static bool ShowPhaseProps(Phase phase, CommandHistory cmd, int minStartFrame = 0, int maxFrameNum = int.MaxValue)
    {
        bool propChanged = ImGuiExt.DragIntHistory("StartFrame", phase.StartFrame, (val) => phase.StartFrame = val, cmd, minValue: minStartFrame, maxValue: maxFrameNum);
        propChanged |= ShowManyKeyFrameProps(phase.KeyFrames, cmd);
        return propChanged;
    }
}