using ImGuiNET;
using T3.Core.Operator;
using T3.Editor.Gui.UiHelpers;
using Vector2 = System.Numerics.Vector2;

namespace T3.Editor.Gui.OpUis.UIs;

public static class IntValueUi
{
    public static OpUi.CustomUiResult DrawChildUi(Instance instance, ImDrawListPtr drawList, ImRect screenRect, Vector2 canvasScale)
    {
        return OpUi.CustomUiResult.None;
    }
/*
    public static OpUi.CustomUiResult DrawChildUi(Instance instance, ImDrawListPtr drawList, ImRect selectableScreenRect, Vector2 canvasScale)
    {
        if (!(instance is IntValue intValueInstance))
            return OpUi.CustomUiResult.None;

        var symbolChild = instance.SymbolChild;
        ImGui.PushClipRect(selectableScreenRect.Min, selectableScreenRect.Max, true);

        var h = selectableScreenRect.GetHeight();
        var font = h > 40
                       ? Fonts.FontLarge
                       : (h > 25
                              ? Fonts.FontNormal
                              : Fonts.FontSmall);

        ImGui.PushFont(font);
        ImGui.SetCursorScreenPos(selectableScreenRect.Min + new Vector2(10,0));
        ImGui.BeginGroup();
        if (!string.IsNullOrWhiteSpace(symbolChild.Name))
        {
            ImGui.TextUnformatted(symbolChild.Name);
        }

        var isAnimated = instance.Parent?.Symbol.Animator.IsInputSlotAnimated(intValueInstance.Int)??false;

        var value = (isAnimated || intValueInstance.Int.HasInputConnections)
                        ? intValueInstance.Int.Value
                        : intValueInstance.Int.TypedInputValue.Value;

        ImGui.TextUnformatted($"{value:0}");
        ImGui.EndGroup();
        ImGui.PopFont();

        ImGui.PopClipRect();
        return OpUi.CustomUiResult.Rendered
               | OpUi.CustomUiResult.PreventOpenSubGraph
               | OpUi.CustomUiResult.PreventInputLabels
               | OpUi.CustomUiResult.PreventTooltip;
    }*/
}