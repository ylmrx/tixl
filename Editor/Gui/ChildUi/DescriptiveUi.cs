using System.IO;
using ImGuiNET;
using T3.Core.Operator;
using T3.Core.Operator.Interfaces;
using T3.Core.Utils;
using T3.Editor.Gui.ChildUi.WidgetUi;
using T3.Editor.Gui.Graph.CustomUi;
using T3.Editor.Gui.UiHelpers;
using T3.Editor.UiModel;

namespace T3.Editor.Gui.ChildUi;

public static class DescriptiveUi
{
    internal static readonly DrawChildUiDelegate DrawChildUiDelegate = DrawChildUi;
    public static OpUi.CustomUiResult DrawChildUi(Instance instance, ImDrawListPtr drawList, ImRect area, Vector2 canvasScale)
    {
        if(instance is not IDescriptiveFilename descriptiveGraphNode)
            return OpUi.CustomUiResult.None;
            
        drawList.PushClipRect(area.Min, area.Max, true);
            
        // Label if instance has title
        var symbolChild = instance.SymbolChild;
            
        WidgetElements.DrawSmallTitle(drawList, area, !string.IsNullOrEmpty(symbolChild.Name) ? symbolChild.Name : symbolChild.Symbol.Name, canvasScale);

        var slot = descriptiveGraphNode.SourcePathSlot;
        var xxx = slot.GetCurrentValue();
        
        var filePath = xxx != null ?  Path.GetFileName(xxx) : "";
            
        WidgetElements.DrawPrimaryValue(drawList, area, filePath, canvasScale);
            
        drawList.PopClipRect();
        return OpUi.CustomUiResult.Rendered | OpUi.CustomUiResult.PreventInputLabels;
    }
}