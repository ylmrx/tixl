using System.Numerics;
using ImGuiNET;

using T3.Core.Operator;
using T3.Core.Utils;
using T3.Editor.Gui.ChildUi.WidgetUi;
using T3.Editor.Gui.Graph.CustomUi;
using T3.Editor.Gui.UiHelpers;
using T3.Editor.UiModel;

namespace libEditor.CustomUi;

public static class SetFloatVarUi
{
    public static OpUi.CustomUiResult DrawChildUi(Instance instance, ImDrawListPtr drawList, ImRect screenRect, Vector2 canvasScale)
    {
        return OpUi.CustomUiResult.None;
    }
/*
    public static SymbolUi.Child.CustomUiResult DrawChildUi(Instance instance1, ImDrawListPtr drawList, ImRect area, Vector2 canvasScale)
    {
        if (!(instance1 is SetFloatVar instance))
            return SymbolUi.Child.CustomUiResult.PreventOpenSubGraph;

        var symbolChild = instance1.SymbolChild;
        drawList.PushClipRect(area.Min, area.Max, true);

        var value = instance.Value.TypedInputValue.Value;

        if (!string.IsNullOrWhiteSpace(symbolChild.Name))
        {
            WidgetElements.DrawPrimaryTitle(drawList, area, symbolChild.Name, canvasScale);
        }
        else
        {
            WidgetElements.DrawPrimaryTitle(drawList, area, "Set " + instance.VariableName.TypedInputValue.Value, canvasScale);
        }

        WidgetElements.DrawSmallValue(drawList, area, $"{value:0.000}", canvasScale);

        drawList.PopClipRect();
        return SymbolUi.Child.CustomUiResult.Rendered | SymbolUi.Child.CustomUiResult.PreventInputLabels | SymbolUi.Child.CustomUiResult.PreventOpenSubGraph;
    }
*/
}