#nullable enable
using ImGuiNET;
using T3.Core.Operator;
using T3.Core.Operator.Slots;
using T3.Editor.Gui.OpUis.WidgetUi;
using T3.Editor.Gui.UiHelpers;

namespace T3.Editor.Gui.OpUis.UIs;

internal static class SetBoolVarUi
{
    private sealed class Binding : OpUiBinding
    {
        internal Binding(Instance instance)
        {
            IsValid = AutoBind(instance);
        }
        
        [BindInput("85CDC809-E835-4851-8956-BB0217B71F1A")]
        internal readonly InputSlot<bool> Value = null!;

        [BindInput("BFDFCD6E-3B31-4B26-AFF4-3023A6B72810")]
        internal readonly InputSlot<string> VariableName = null!;

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


        var symbolChild = instance.SymbolChild;
        drawList.PushClipRect(area.Min, area.Max, true);

        var value = data.Value.TypedInputValue.Value;

        if (!string.IsNullOrWhiteSpace(symbolChild.Name))
        {
            WidgetElements.DrawPrimaryTitle(drawList, area, symbolChild.Name, canvasScale);
        }
        else
        {
            WidgetElements.DrawPrimaryTitle(drawList, area, "Bool " + data.VariableName.TypedInputValue.Value + " =", canvasScale);
        }

        WidgetElements.DrawSmallValue(drawList, area, $"{value}", canvasScale);

        drawList.PopClipRect();
        return OpUi.CustomUiResult.Rendered | OpUi.CustomUiResult.PreventInputLabels | OpUi.CustomUiResult.PreventOpenSubGraph;
    }
}