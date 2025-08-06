#nullable enable
using ImGuiNET;
using T3.Core.Operator;
using T3.Core.Operator.Slots;
using T3.Editor.Gui.OpUis.WidgetUi;
using T3.Editor.Gui.UiHelpers;

namespace T3.Editor.Gui.OpUis.UIs;

internal static class GetBoolVarUi
{
    private sealed class Binding : OpUiBinding
    {
        internal Binding(Instance instance)
        {
            IsValid = AutoBind(instance);
        }

        [BindInput("b0821091-68c0-4e34-9f8b-926c0b6ebf94")]
        internal readonly InputSlot<string> VariableName = null!;

        [BindOutput("B5BB6CD9-58DD-4C9C-AAB3-AE0E21C81822")]
        internal readonly Slot<bool> Result = null!;
    }

    public static OpUi.CustomUiResult DrawChildUi(Instance instance,
                                                  ImDrawListPtr drawList,
                                                  ImRect area,
                                                  Vector2 canvasScale,
                                                  ref OpUiBinding? data1)
    {
        data1 ??= new Binding(instance);
        var data = (Binding)data1;

        if (!data.IsValid)
            return OpUi.CustomUiResult.PreventOpenSubGraph;

        drawList.PushClipRect(area.Min, area.Max, true);

        var value = data.Result.Value;

        var name = instance.SymbolChild.Name;
        if (!string.IsNullOrWhiteSpace(name))
        {
            WidgetElements.DrawPrimaryTitle(drawList, area, name, canvasScale);
        }
        else
        {
            WidgetElements.DrawPrimaryTitle(drawList, area, "Get " + data.VariableName.TypedInputValue.Value, canvasScale);
        }

        WidgetElements.DrawSmallValue(drawList, area, $"{value}", canvasScale);

        drawList.PopClipRect();
        return OpUi.CustomUiResult.Rendered | OpUi.CustomUiResult.PreventInputLabels | OpUi.CustomUiResult.PreventOpenSubGraph;
    }
}