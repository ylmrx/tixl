using ImGuiNET;
using T3.Core.Operator;
using T3.Editor.Gui.UiHelpers;

namespace T3.Editor.Gui.OpUis.UIs;

public static class SampleCurveUi
{
    public static OpUi.CustomUiResult DrawChildUi(Instance instance, ImDrawListPtr drawList, ImRect screenRect, Vector2 canvasScale)
    {
        return OpUi.CustomUiResult.None;
    }
/*
    public static OpUi.CustomUiResult DrawChildUi(Instance instance, ImDrawListPtr drawList, ImRect selectableScreenRect, Vector2 canvasScale)
    {
        if (!(instance is SampleCurve sampleCurve))
            return OpUi.CustomUiResult.None;

        var dragWidth = WidgetElements.DrawOperatorDragHandle(selectableScreenRect, drawList, canvasScale);
        var innerRect = selectableScreenRect;
        innerRect.Min.X += dragWidth;
        innerRect.Min.Y += 1;

        if (innerRect.GetHeight() < 0)
            return OpUi.CustomUiResult.PreventTooltip
                   | OpUi.CustomUiResult.PreventOpenSubGraph
                   | OpUi.CustomUiResult.PreventInputLabels
                   | OpUi.CustomUiResult.PreventOpenParameterPopUp;

        var curve = (sampleCurve.Curve.HasInputConnections)
                        ? sampleCurve.Curve.Value
                        :sampleCurve.Curve.TypedInputValue.Value;

        //var curve = sampleCurve.Curve.Value;
        if (curve == null)
        {
            //Log.Warning("Can't draw undefined gradient");
            return OpUi.CustomUiResult.PreventTooltip
                   | OpUi.CustomUiResult.PreventOpenSubGraph
                   | OpUi.CustomUiResult.PreventInputLabels
                   | OpUi.CustomUiResult.PreventOpenParameterPopUp;
        }

        ImGui.PushClipRect(innerRect.Min, innerRect.Max, true);
        ImGui.SetCursorScreenPos(innerRect.Min) ;
        ImGui.BeginChild("curve" + instance.SymbolChildId.GetHashCode(), innerRect.GetSize(), false, ImGuiWindowFlags.NoScrollbar);
        {
            var cloneIfModified = sampleCurve.Curve.Input.IsDefault;

            var preventEditingUnlessCtrlPressed = ImGui.GetIO().KeyCtrl
                                                      ? T3Ui.EditingFlags.None
                                                      : T3Ui.EditingFlags.PreventMouseInteractions;

            var keepPositionForIcon = ImGui.GetCursorPos() + Vector2.One;
            var modified2 = CurveInputEditing.DrawCanvasForCurve(ref curve,
                                                                 sampleCurve.Curve.Input,
                                                                 cloneIfModified,
                                                                 instance.Parent, T3Ui.EditingFlags.ExpandVertically
                                                                                  | preventEditingUnlessCtrlPressed
                                                                                  | T3Ui.EditingFlags.PreventZoomWithMouseWheel);

            var showPopupIcon = innerRect.GetHeight()> ImGui.GetFrameHeight()* T3Ui.UiScaleFactor * 2;
            if (showPopupIcon && CurveEditPopup.DrawPopupIndicator(instance.Parent, sampleCurve.Curve.Input, ref curve, keepPositionForIcon, cloneIfModified, out var popupResult))
            {
                modified2 = popupResult;
            }

            if ((modified2 & InputEditStateFlags.Modified) != InputEditStateFlags.Nothing)
            {
                if (cloneIfModified)
                {
                    sampleCurve.Curve.SetTypedInputValue(curve);
                }
                sampleCurve.Result.DirtyFlag.Invalidate();
                sampleCurve.CurveOutput.DirtyFlag.ForceInvalidate();
            }

            DrawSamplePointIndicator();
        }
        ImGui.EndChild();
        ImGui.PopClipRect();

        return OpUi.CustomUiResult.Rendered
               | OpUi.CustomUiResult.PreventTooltip
               | OpUi.CustomUiResult.PreventOpenSubGraph
               | OpUi.CustomUiResult.PreventInputLabels
               | OpUi.CustomUiResult.PreventOpenParameterPopUp;

        void DrawSamplePointIndicator()
        {
            ICanvas canvas = null;//CurveInputEditing.GetCanvasForCurve(curve);
            if (canvas == null)
                return;

            var x = canvas.TransformPosition(new Vector2(sampleCurve.U.Value, 0)).X;
            if (!(x >= innerRect.Min.X) || !(x < innerRect.Max.X))
                return;

            var pMin = new Vector2(x, innerRect.Min.Y);
            var pMax = new Vector2(x + 1, innerRect.Max.Y);
            drawList.AddRectFilled(pMin, pMax, UiColors.StatusAnimated);
        }
    }*/
}