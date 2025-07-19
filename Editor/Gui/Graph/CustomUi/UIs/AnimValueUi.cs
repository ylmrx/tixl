#nullable enable
using System.Reflection;
using ImGuiNET;
using T3.Core.Operator;
using T3.Core.Operator.Slots;
using T3.Core.Utils;
using T3.Editor.Gui.ChildUi.WidgetUi;
using T3.Editor.Gui.Styling;
using T3.Editor.Gui.UiHelpers;

namespace T3.Editor.Gui.Graph.CustomUi.UIs;

// ReSharper disable once UnusedType.Global
internal abstract class AnimValueUi 
{
    //private static readonly Guid _symbolId = OpUi.RegisterUi(new Guid("ea7b8491-2f8e-4add-b0b1-fd068ccfed0d"), DrawChildUi);

    private sealed class ParamSet : CustomUiParamSet
    {
        internal ParamSet(Instance instance)
        {
            if (OpUi.TryGetInput(instance, new Guid("48005727-0158-4795-ad70-8410c27fd01d"), out Rate!) &&
                OpUi.TryGetInput(instance, new Guid("4cf5d20b-7335-4584-b246-c260ac5cdf4f"), out Shape!) &&
                OpUi.TryGetInput(instance, new Guid("79917ef7-64ca-4825-9c6a-c9b2a7f6ff86"), out Amplitude!) &&
                OpUi.TryGetInput(instance, new Guid("ddd93b06-118e-43e0-85f6-c150faf91d04"), out Offset!) &&
                OpUi.TryGetInput(instance, new Guid("f12fee9a-dd91-40c2-9aa5-ea34804a858d"), out Bias!) &&
                OpUi.TryGetInput(instance, new Guid("8327e7ec-4370-4a3e-bd69-db3f4aa4b1d7"), out Ratio!) &&
                OpUi.TryGetField(instance, "_normalizedTime", out _normalizedTimeField))
            {
                IsValid = true;
            }
            else
            {
                Shape = null!;
                Rate = Amplitude = Offset = Bias = Ratio = null!; // will never be used if !IsValid
            }

            _instance = instance;
        }

        private readonly FieldInfo? _normalizedTimeField;
        private readonly Instance _instance;
        internal double NormalizedTime => (double)(_normalizedTimeField?.GetValue(_instance) ?? 0);

        internal readonly InputSlot<float> Rate;
        internal readonly InputSlot<int> Shape;
        internal readonly InputSlot<float> Amplitude;
        internal readonly InputSlot<float> Offset;
        internal readonly InputSlot<float> Bias;
        internal readonly InputSlot<float> Ratio;
    }
    

    public static OpUi.CustomUiResult DrawChildUi(Instance instance, ImDrawListPtr drawList, ImRect screenRect, Vector2 canvasScale)
    {
        // if (instance.Symbol.Id != _symbolId
        //     || !ImGui.IsRectVisible(screenRect.Min, screenRect.Max))
        // {
        //     return OpUi.CustomUiResult.None;
        // }

        // Should be cached later...
        var pars = new ParamSet(instance);
        if (!pars.IsValid)
            return OpUi.CustomUiResult.None;

        var isNodeActivated = false;
        ImGui.PushID(instance.SymbolChildId.GetHashCode());
        if (WidgetElements.DrawRateLabelWithTitle(pars.Rate,
                                                  screenRect,
                                                  drawList,
                                                  "Anim " + (AnimMath.Shapes)pars.Shape.TypedInputValue.Value, canvasScale))
        {
            isNodeActivated = true;
            // animValue.Rate.Input.IsDefault = false;
            // animValue.Rate.DirtyFlag.Invalidate();
        }

        // Graph dragging to edit Bias and Ratio
        var h = screenRect.GetHeight();
        var graphRect = screenRect;

        const float relativeGraphWidth = 0.75f;

        graphRect.Expand(-3);
        graphRect.Min.X = graphRect.Max.X - graphRect.GetWidth() * relativeGraphWidth;

        var highlightEditable = ImGui.GetIO().KeyCtrl;

        if (h > 14 * T3Ui.UiScaleFactor)
        {
            isNodeActivated |= ValueLabel.Draw(drawList, graphRect, new Vector2(1, 0), pars.Amplitude);
            isNodeActivated |= ValueLabel.Draw(drawList, graphRect, new Vector2(1, 1), pars.Offset);
        }

        var isGraphActive = false;
        ImGui.SetCursorScreenPos(graphRect.Min);
        if (ImGui.GetIO().KeyCtrl)
        {
            ImGui.InvisibleButton("dragMicroGraph", graphRect.GetSize());

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup)
                && ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsItemActive())
            {
                isGraphActive = true;
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
            }
        }

        if (isGraphActive)
        {
            isNodeActivated = true;
            var dragDelta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 1);

            if (ImGui.IsItemActivated())
            {
                //_dragStartPosition = ImGui.GetMousePos();
                _dragStartBias = pars.Bias.TypedInputValue.Value;
                _dragStartRatio = pars.Ratio.TypedInputValue.Value;
            }

            if (Math.Abs(dragDelta.X) > 0.5f)
            {
                pars.Ratio.TypedInputValue.Value = (_dragStartRatio + dragDelta.X / 100f).Clamp(0.001f, 1f);
                pars.Ratio.DirtyFlag.Invalidate();
                pars.Ratio.Input.IsDefault = false;
            }

            if (Math.Abs(dragDelta.Y) > 0.5f)
            {
                pars.Bias.TypedInputValue.Value = (_dragStartBias - dragDelta.Y / 100f).Clamp(0.01f, 0.99f);
                pars.Bias.DirtyFlag.Invalidate();
                pars.Bias.Input.IsDefault = false;
            }
        }

        // Draw Curve
        {
            var graphWidth = graphRect.GetWidth();
            var h1 = graphRect.GetHeight();

            var shapeIndex = pars.Shape.HasInputConnections // Todo check for animated 
                                 ? pars.Shape.Value
                                 : pars.Shape.TypedInputValue.Value;

            var shape1 = (AnimMath.Shapes)shapeIndex.Clamp(0, Enum.GetNames(typeof(AnimMath.Shapes)).Length);

            // Draw Graph
            {
                const float previousCycleFragment = 0.25f;
                const float relativeX = previousCycleFragment / (1 + previousCycleFragment);

                // Horizontal line
                var lh1 = graphRect.Min + Vector2.UnitY * h1 / 2;
                var lh2 = new Vector2(graphRect.Max.X, lh1.Y + 1);
                drawList.AddRectFilled(lh1, lh2, UiColors.WidgetAxis);

                // Vertical start line 
                var lv1 = graphRect.Min + Vector2.UnitX * (int)(graphWidth * relativeX);
                var lv2 = new Vector2(lv1.X + 1, graphRect.Max.Y);
                drawList.AddRectFilled(lv1, lv2, UiColors.WidgetAxis);

                // Fragment line 
                var cycleWidth = graphWidth * (1 - relativeX);
                var normalizedTime = pars.NormalizedTime;
                var dx = new Vector2(MathUtils.Fmod((float)normalizedTime, 1f) * cycleWidth - 1, 0);

                drawList.AddRectFilled(lv1 + dx, lv2 + dx, UiColors.WidgetActiveLine);

                // Draw graph
                //        lv
                //        |  2-------3    y
                //        | /
                //  0-----1 - - - - - -   lh
                //        |
                //        |

                for (var i = 0; i < GraphListSteps; i++)
                {
                    var f = (float)i / GraphListSteps;
                    var fragment = f * (1 + previousCycleFragment) - previousCycleFragment + Math.Floor(normalizedTime);

                    var v = AnimMath.CalcValueForNormalizedTime(shape1,
                                                                fragment,
                                                                0,
                                                                pars.Bias.TypedInputValue.Value,
                                                                pars.Ratio.TypedInputValue.Value).Clamp(-1, 1);
                    var vv = (0.5f - v / 2) * h1;

                    _graphLinePoints[i] = new Vector2(f * graphWidth,
                                                      vv
                                                     ) + graphRect.Min;
                }

                var curveLineColor = highlightEditable ? UiColors.WidgetLineHover : UiColors.WidgetLine;
                drawList.AddPolyline(ref _graphLinePoints[0], GraphListSteps, curveLineColor, ImDrawFlags.None, 1.5f);
            }
        }

        ImGui.PopID();

        return OpUi.CustomUiResult.Rendered
               | OpUi.CustomUiResult.PreventOpenSubGraph
               | OpUi.CustomUiResult.PreventInputLabels
               | OpUi.CustomUiResult.PreventTooltip
               | (isNodeActivated ? OpUi.CustomUiResult.IsActive : OpUi.CustomUiResult.None);
    }

    private static float _dragStartBias;
    private static float _dragStartRatio;

    private static readonly Vector2[] _graphLinePoints = new Vector2[GraphListSteps];
    private const int GraphListSteps = 80;
}