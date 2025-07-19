using System.Reflection;
using ImGuiNET;
using T3.Core.Operator;
using T3.Core.Operator.Slots;
using T3.Editor.Gui.OpUis.WidgetUi;
using T3.Editor.Gui.UiHelpers;

namespace T3.Editor.Gui.OpUis.UIs;

internal static class Jitter2dUi
{
    private sealed class ParamSet : CustomUiParamSet
    {
        internal ParamSet(Instance instance)
        {
            if (OpUi.TryGetInput(instance, new Guid("1DF95BEB-DA6D-4263-8273-7A180FD190F5"), out Rate) &&
                OpUi.TryGetInput(instance, new Guid("F101AF0C-DE31-4AFB-ACB4-8166C62C2EC8"), out JumpDistance) &&
                OpUi.TryGetInput(instance, new Guid("38086D8A-15E0-4F3E-B161-A46A79FC5CC3"), out Blending) &&
                OpUi.TryGetProperty(instance, "Fragment", out _fragmentProp)
               )
            {
                IsValid = true;
            }

            _instance = instance;
        }

        private readonly PropertyInfo _fragmentProp;
        private readonly Instance _instance;
        internal float Fragment => (float)(_fragmentProp?.GetValue(_instance) ?? 0);

        internal readonly InputSlot<float> Rate;
        internal readonly InputSlot<float> JumpDistance;
        internal readonly InputSlot<float> Blending;
    }

    public static OpUi.CustomUiResult DrawChildUi(Instance instance,
                                                  ImDrawListPtr drawList,
                                                  ImRect screenRect,
                                                  Vector2 canvasScale,
                                                  ref CustomUiParamSet data1)
    {
        data1 ??= new ParamSet(instance);
        var data = (ParamSet)data1;

        if (!data.IsValid)
            return OpUi.CustomUiResult.None;

        if (WidgetElements.DrawRateLabelWithTitle(data.Rate, screenRect, drawList, "Jitter2D", canvasScale))
        {
            data.Rate.Input.IsDefault = false;
            data.Rate.DirtyFlag.Invalidate();
        }

        var label = $"±{data.JumpDistance.TypedInputValue.Value:0.0}";

        if (MicroGraph.Draw(ref data.JumpDistance.TypedInputValue.Value,
                            ref data.Blending.TypedInputValue.Value,
                            data.Fragment,
                            screenRect, drawList, label))
        {
            data.Blending.Input.IsDefault = false;
            data.Blending.DirtyFlag.Invalidate();
            data.JumpDistance.Input.IsDefault = false;
            data.JumpDistance.DirtyFlag.Invalidate();
        }

        return OpUi.CustomUiResult.Rendered | OpUi.CustomUiResult.PreventInputLabels;
    }
}