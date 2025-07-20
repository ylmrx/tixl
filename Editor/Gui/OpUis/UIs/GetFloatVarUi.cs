#nullable enable
using ImGuiNET;
using T3.Core.Operator;
using T3.Core.Operator.Slots;
using T3.Editor.Gui.OpUis.WidgetUi;
using T3.Editor.Gui.UiHelpers;

namespace T3.Editor.Gui.OpUis.UIs;

internal static class GetFloatVarUi
{
    private sealed class Binding : OpUiBinding
    {
        internal Binding(Instance instance)
        {
            IsValid = AutoBind(instance);
        }

        [BindInput("015d1ea0-ea51-4038-893a-4af2f8584631")]
        internal readonly InputSlot<string> VariableName = null!;

        [BindOutput("e368ba33-827e-4e08-aa19-ba894b40906a")]
        internal readonly Slot<float> Result = null!;
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

        WidgetElements.DrawSmallValue(drawList, area, $"{value:0.000}", canvasScale);

        drawList.PopClipRect();
        return OpUi.CustomUiResult.Rendered | OpUi.CustomUiResult.PreventInputLabels | OpUi.CustomUiResult.PreventOpenSubGraph;
    }
}