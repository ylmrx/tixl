using ImGuiNET;
using T3.Core.Operator;
using T3.Editor.Gui.UiHelpers;

namespace T3.Editor.Gui.OpUis.UIs;

public static class GetFloatVarUi
{
    public static OpUi.CustomUiResult DrawChildUi(Instance instance, ImDrawListPtr drawList, ImRect screenRect, Vector2 canvasScale)
    {
        return OpUi.CustomUiResult.None;
    }
/*


    public static OpUi.CustomUiResult DrawChildUi(Instance instance1, ImDrawListPtr drawList, ImRect area, Vector2 canvasScale)
    {
        if (!(instance1 is GetFloatVar instance))
            return OpUi.CustomUiResult.PreventOpenSubGraph;

        drawList.PushClipRect(area.Min, area.Max, true);

        var value = instance.Result.Value;

        var name = instance1.SymbolChild.Name;
        if (!string.IsNullOrWhiteSpace(instance1.SymbolChild.Name))
        {
            WidgetElements.DrawPrimaryTitle(drawList, area, name, canvasScale);
        }
        else
        {
            WidgetElements.DrawPrimaryTitle(drawList, area, "Get " + instance.VariableName.TypedInputValue.Value, canvasScale);
        }

        WidgetElements.DrawSmallValue(drawList, area, $"{value:0.000}", canvasScale);

        drawList.PopClipRect();
        return OpUi.CustomUiResult.Rendered | OpUi.CustomUiResult.PreventInputLabels | OpUi.CustomUiResult.PreventOpenSubGraph;
    }
    */
}