#nullable enable
using ImGuiNET;
using T3.Core.Operator.Slots;
using T3.Core.Utils;
using T3.Editor.Gui.Styling;

namespace T3.Editor.Gui.OutputUi;

internal sealed class FloatListOutputUi : OutputUi<List<float>>
{
    public override IOutputUi Clone()
    {
        return new FloatListOutputUi()
                   {
                       OutputDefinition = OutputDefinition,
                       PosOnCanvas = PosOnCanvas,
                       Size = Size
                   };
    }

    private enum ViewStyles
    {
        List,
        Grid,
        Plot,
        PlotAutoScale,
    }

    private sealed class ViewSettings
    {
        public ViewStyles ViewStyle = ViewStyles.Plot;

        public int GridColumns = 16;
        public float MinFit = -1;
        public float MaxFit = 1;
        public float _lastMin;
        public float _lastMax;
        public double _lastSum;
    }

    private static readonly Dictionary<string, ViewSettings> _viewSettingsForId = [];

    protected override void DrawTypedValue(ISlot slot, string viewId)
    {
        if (slot is not Slot<List<float>> valueListSlot)
        {
            ImGui.TextUnformatted("Not a value list?");
            return;
        }

        var valueList = valueListSlot.Value;
        if (valueList == null)
        {
            ImGui.TextUnformatted("List is undefined (null)");
            return;
        }

        viewId ??= string.Empty;
        var viewSettings = _viewSettingsForId.TryGetValue(viewId, out var settings)
                               ? settings
                               : _viewSettingsForId[viewId] = new ViewSettings();

        FormInputs.DrawEnumDropdown(ref viewSettings.ViewStyle, "##view style");

        switch (viewSettings.ViewStyle)
        {
            case ViewStyles.List:
                break;

            case ViewStyles.Grid:
            {
                var indexColumnWidth = 80 * T3Ui.UiScaleFactor;
                ImGui.NewLine();

                // Draw Header
                for (var index = 0; index < viewSettings.GridColumns; index++)
                {
                    ImGui.SameLine(50 * T3Ui.UiScaleFactor * index + indexColumnWidth);
                    CustomComponents.StylizedText($"{index}", Fonts.FontSmall, UiColors.TextMuted);
                }

                int columnIndex = 0;
                var min = float.PositiveInfinity;
                var max = float.NegativeInfinity;

                for (var index = 0; index < valueList.Count; index++)
                {
                    if (columnIndex == 0)
                    {
                        CustomComponents.StylizedText($"#{index}", Fonts.FontSmall, UiColors.TextMuted);
                        //ImGui.SameLine();
                        //ImGui.TextUnformatted($"#{index}");    
                    }

                    ImGui.SameLine(50 * T3Ui.UiScaleFactor * columnIndex + indexColumnWidth);

                    var v = valueList[index];
                    min = MathF.Min(min, v);
                    max = MathF.Max(max, v);
                    //ImGui.TextUnformatted($"{v:0.00}");

                    var n = v < 0
                                ? (v / viewSettings._lastMax)
                                : (v / viewSettings._lastMin);

                    var opacity = (n + 0.1f).Clamp(0, 1);
                    var color = v < 0 ? UiColors.StatusAttention : UiColors.Text;
                    CustomComponents.StylizedText($"{v:0.0}", Fonts.FontSmall, color.Fade(opacity));
                    columnIndex++;

                    if (columnIndex == viewSettings.GridColumns)
                    {
                        //ImGui.NewLine();
                        columnIndex = 0;
                    }
                }

                viewSettings._lastMax = max;
                viewSettings._lastMin = max;

                break;
            }
            case ViewStyles.Plot:
            {
                if (valueList.Count > 1)
                {
                    var plotLength = Math.Min(MaxPlotValueCount, valueList.Count);
                    valueList.CopyTo(0, _plotArray, 0, plotLength);

                    ImGui.PlotLines("##values", ref _plotArray[0],
                                    plotLength,
                                    0,
                                    "",
                                    viewSettings.MinFit,
                                    viewSettings.MaxFit,
                                    new Vector2(ImGui.GetContentRegionAvail().X, 200));

                    FormInputs.AddFloat("Max", ref viewSettings.MaxFit);
                    FormInputs.AddFloat("Min", ref viewSettings.MinFit);
                }

                if (valueList.Count > 0)
                {
                    var min = float.PositiveInfinity;
                    var max = float.NegativeInfinity;
                    var sum = 0f;
                    foreach (var number in valueList)
                    {
                        sum += number;
                        min = Math.Min(min, number);
                        max = Math.Max(max, number);
                    }

                    ImGui.TextUnformatted($"{valueList.Count}  between {min:G5} .. {max:G5}  avg {sum / valueList.Count:G5}");
                }

                break;
            }

            case ViewStyles.PlotAutoScale:
            {
                var plotLength = Math.Min(MaxPlotValueCount, valueList.Count);
                valueList.CopyTo(0, _plotArray, 0, plotLength);

                ImGui.PlotLines("##values", ref _plotArray[0], plotLength);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private const int MaxPlotValueCount = 1024;
    private static readonly float[] _plotArray = new float[MaxPlotValueCount];
}