using ImGuiNET;
using T3.Core.Operator;
using T3.Editor.Gui.UiHelpers;

namespace T3.Editor.Gui.OpUis.UIs;

public static class _JitterUi
{
    public static OpUi.CustomUiResult DrawChildUi(Instance instance, ImDrawListPtr drawList, ImRect screenRect, Vector2 canvasScale)
    {
        return OpUi.CustomUiResult.None;
        // if (!(instance is _Jitter jitter))
        //     return OpUi.CustomUiResult.None;
        //     
        // if (WidgetElements.DrawRateLabelWithTitle(jitter.Rate, screenRect, drawList, nameof(jitter), canvasScale))
        // {
        //     jitter.Rate.Input.IsDefault = false;
        //     jitter.Rate.DirtyFlag.Invalidate();
        // }
        // var label = $"±{jitter.JumpDistance.TypedInputValue.Value:0.0}";
        //     
        // if (MicroGraph.Draw(ref jitter.JumpDistance.TypedInputValue.Value, 
        //                     ref jitter.Blending.TypedInputValue.Value, 
        //                     jitter.Fragment, 
        //                     screenRect, drawList, label))
        // {
        //     jitter.Blending.Input.IsDefault = false;
        //     jitter.Blending.DirtyFlag.Invalidate();
        //     jitter.JumpDistance.Input.IsDefault = false;
        //     jitter.JumpDistance.DirtyFlag.Invalidate();
        // }
        // return OpUi.CustomUiResult.Rendered | OpUi.CustomUiResult.PreventInputLabels;
    }
}