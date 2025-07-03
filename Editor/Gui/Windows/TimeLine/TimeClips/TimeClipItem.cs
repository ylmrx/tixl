#nullable enable
using ImGuiNET;
using T3.Core.Animation;
using T3.Core.DataTypes.Vector;
using T3.Core.Operator;
using T3.Editor.Gui.Styling;
using T3.Editor.Gui.UiHelpers;
using T3.Editor.UiModel;
using T3.Editor.UiModel.Commands;
using T3.Editor.UiModel.Commands.Animation;
using T3.Editor.UiModel.ProjectHandling;
using T3.Editor.UiModel.Selection;

namespace T3.Editor.Gui.Windows.TimeLine.TimeClips;

internal static class TimeClipItem
{
    /// <summary>
    /// Attributes required and identically for drawing and handling all time clip items of a canvas for the current frame.
    /// </summary>
    public record struct ClipDrawingAttributes(
        LayersArea.LayerContext LayerContext,
        ImRect LayerRect,
        int MinLayerIndex,
        Instance CompositionOp,
        SymbolUi CompositionSymbolUi,
        MoveTimeClipsCommand? MoveClipsCommand,
        ImDrawListPtr DrawList);

    internal static void DrawClip(ITimeClip timeClip, ref ClipDrawingAttributes attr)
    {
        var xStartTime = attr.LayerContext.TimeCanvas.TransformX(timeClip.TimeRange.Start) + 1;
        var xEndTime = attr.LayerContext.TimeCanvas.TransformX(timeClip.TimeRange.End) + 1;
        var position = new Vector2(xStartTime,
                                   attr.LayerRect.Min.Y + (timeClip.LayerIndex - attr.MinLayerIndex) * LayersArea.LayerHeight);

        var clipWidth = xEndTime - xStartTime;
        var showSizeHandles = clipWidth > 4 * HandleWidth;
        var bodyWidth = showSizeHandles
                            ? (clipWidth - 2 * HandleWidth)
                            : clipWidth;

        var bodySize = new Vector2(bodyWidth, LayersArea.LayerHeight - 2);
        var clipSize = new Vector2(clipWidth, LayersArea.LayerHeight - 2);

        var symbolChildUi = attr.CompositionSymbolUi.ChildUis[timeClip.Id];

        ImGui.PushID(symbolChildUi.Id.GetHashCode());

        var isSelected = attr.LayerContext.ClipSelection.SelectedClips.Contains(timeClip);
        var itemRectMax = position + clipSize - new Vector2(1, 0);

        var rounding = 3.5f;
        var randomColor = DrawUtils.RandomColorForHash(timeClip.Id.GetHashCode());

        attr.DrawList.AddRectFilled(position, itemRectMax, randomColor.Fade(0.25f), rounding);

        var timeRemapped = timeClip.TimeRange != timeClip.SourceRange;
        var timeStretched = Math.Abs(timeClip.TimeRange.Duration - timeClip.SourceRange.Duration) > 0.001;
        if (timeStretched)
        {
            attr.DrawList.AddRectFilled(position + new Vector2(2, clipSize.Y - 4),
                                        position + new Vector2(clipSize.X - 3, clipSize.Y - 2),
                                        UiColors.StatusAttention, rounding);
        }
        else if (timeRemapped)
        {
            attr.DrawList.AddRectFilled(position + new Vector2(0, clipSize.Y - 2),
                                        position + new Vector2(clipSize.X - 1, clipSize.Y-1),
                                        UiColors.StatusAnimated);
        }

        if (isSelected)
            attr.DrawList.AddRect(position, itemRectMax, UiColors.Selection, rounding);

        ImGui.PushClipRect(position, itemRectMax - new Vector2(3, 0), true);
        var label = timeStretched
                        ? symbolChildUi.SymbolChild.ReadableName + $" ({GetSpeed(timeClip)}%)"
                        : symbolChildUi.SymbolChild.ReadableName;
        ImGui.PushFont(Fonts.FontSmall);
        attr.DrawList.AddText(position + new Vector2(4, 1), isSelected ? UiColors.Selection : randomColor, label);
        ImGui.PopFont();
        ImGui.PopClipRect();

        if (isSelected && timeRemapped && attr.LayerContext.ClipSelection.Count == 1)
        {
            var verticalOffset = ImGui.GetContentRegionMax().Y + ImGui.GetWindowPos().Y - position.Y - LayersArea.LayerHeight;
            var horizontalOffset = attr.LayerContext.TimeCanvas.TransformDirection(new Vector2(timeClip.SourceRange.Start - timeClip.TimeRange.Start, 0)).X;
            var startPosition = position + new Vector2(0, LayersArea.LayerHeight);
            attr.DrawList.AddBezierCubic(startPosition,
                                         startPosition + new Vector2(0, verticalOffset),
                                         startPosition + new Vector2(horizontalOffset, 0),
                                         startPosition + new Vector2(horizontalOffset, verticalOffset),
                                         _timeRemappingColor, 1);

            horizontalOffset = attr.LayerContext.TimeCanvas.TransformDirection(new Vector2(timeClip.SourceRange.End - timeClip.TimeRange.End, 0)).X;
            var endPosition = position + new Vector2(clipSize.X, LayersArea.LayerHeight);
            attr.DrawList.AddBezierCubic(endPosition,
                                         endPosition + new Vector2(0, verticalOffset),
                                         endPosition + new Vector2(horizontalOffset, 0),
                                         endPosition + new Vector2(horizontalOffset, verticalOffset),
                                         _timeRemappingColor, 1);
        }

        ImGui.SetCursorScreenPos(showSizeHandles ? (position + _handleOffset) : position);

        var wasClicked = ImGui.InvisibleButton("body", bodySize);

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            {
                ImGui.TextUnformatted(symbolChildUi.SymbolChild.ReadableName);

                ImGui.PushStyleColor(ImGuiCol.Text, UiColors.TextMuted.Rgba);
                ImGui.TextUnformatted($"Visible: {timeClip.TimeRange.Start:0.00} ... {timeClip.TimeRange.End:0.00}");
                if (timeRemapped)
                {
                    ImGui.TextUnformatted($"Source {timeClip.SourceRange.Start:0.00} ... {timeClip.SourceRange.End:0.00}");
                }

                if (timeStretched)
                {
                    var speed = GetSpeed(timeClip);
                    ImGui.TextUnformatted($"Speed: {speed:0.}%");
                }

                ImGui.PopStyleColor();
            }
            ImGui.EndTooltip();
        }

        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(0))
        {
            if (Structure.TryGetUiAndInstanceInComposition(timeClip.Id, attr.CompositionOp, out _, out var instance))
            {
                if (instance.Symbol.Children.Count > 0)
                    attr.LayerContext.RequestChildComposition(instance.SymbolChildId);
            }
        }

        if (ImGui.IsItemHovered())
        {
            FrameStats.AddHoveredId(symbolChildUi.Id);
        }

        var notClickingOrDragging = !ImGui.IsItemActive() && !ImGui.IsMouseDragging(ImGuiMouseButton.Left);
        if (notClickingOrDragging && attr.MoveClipsCommand != null)
        {
            // Store values and nullify command
            attr.LayerContext.TimeCanvas.CompleteDragCommand();
        }

        if (wasClicked)
        {
            if (ImGui.GetIO().KeyAlt)
            {
                ImGui.OpenPopup(_TimeEditPopupId);
            }
            else
            {
                FitViewToSelectionHandling.FitViewToSelection();
            }
        }

        HandleDragging(attr, timeClip, isSelected, wasClicked, HandleDragMode.Body);

        var handleSize = showSizeHandles ? new Vector2(HandleWidth, LayersArea.LayerHeight) : Vector2.One;

        ImGui.SetCursorScreenPos(position);
        var aHandleClicked = ImGui.InvisibleButton("startHandle", handleSize);
        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
        {
            attr.DrawList.AddRectFilled(ImGui.GetItemRectMin() + new Vector2(2, 3),
                                        ImGui.GetItemRectMax() - new Vector2(1, 4),
                                        UiColors.ForegroundFull.Fade(0.3f),
                                        5);
        }

        HandleDragging(attr, timeClip, isSelected, false, HandleDragMode.Start);

        ImGui.SetCursorScreenPos(position + new Vector2(bodyWidth + HandleWidth, 0));
        aHandleClicked |= ImGui.InvisibleButton("endHandle", handleSize);
        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
        {
            attr.DrawList.AddRectFilled(ImGui.GetItemRectMin() + new Vector2(0, 3),
                                        ImGui.GetItemRectMax() - new Vector2(3, 4),
                                        UiColors.ForegroundFull.Fade(0.3f),
                                        5);
        }

        HandleDragging(attr, timeClip, isSelected, false, HandleDragMode.End);

        if (aHandleClicked)
        {
            attr.LayerContext.TimeCanvas.CompleteDragCommand();

            if (attr.MoveClipsCommand != null)
            {
                attr.MoveClipsCommand.StoreCurrentValues();
                UndoRedoStack.Add(attr.MoveClipsCommand);
                attr.MoveClipsCommand = null;
            }
        }
        DrawTimeEditPop(timeClip);

        ImGui.PopID();
    }

    private static void DrawTimeEditPop(ITimeClip item)
    {
        if (ImGui.BeginPopup(_TimeEditPopupId))
        {
            ImGui.TextUnformatted("Some content for later.");
            ImGui.EndPopup();
        }
    }

    private const string _TimeEditPopupId = nameof(_TimeEditPopupId); 
    

    private static double GetSpeed(ITimeClip timeClip)
    {
        return Math.Abs(timeClip.TimeRange.Duration) > 0.001
                   ? Math.Round((timeClip.TimeRange.Duration / timeClip.SourceRange.Duration) * 100)
                   : 9999;
    }

    private enum HandleDragMode
    {
        Body = 0,
        Start,
        End,
    }

    private static void HandleDragging(ClipDrawingAttributes attr, ITimeClip timeClip, bool isSelected, bool wasClicked, HandleDragMode mode)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(mode == HandleDragMode.Body
                                     ? ImGuiMouseCursor.Hand
                                     : ImGuiMouseCursor.ResizeEW);
        }

        if (!wasClicked && (!ImGui.IsItemActive() || !ImGui.IsMouseDragging(0, UserSettings.Config.ClickThreshold)))
            return;

        if (ImGui.GetIO().KeyCtrl)
        {
            if (isSelected)
            {
                attr.LayerContext.ClipSelection.Deselect(timeClip);
            }

            return;
        }

        if (!isSelected)
        {
            if (!ImGui.GetIO().KeyShift)
            {
                attr.LayerContext.TimeCanvas.ClearSelection();
            }

            attr.LayerContext.ClipSelection.Select(timeClip);
        }

        var mousePos = ImGui.GetIO().MousePos;
        var dragContent = false;// ImGui.GetIO().KeyAlt;
        var referenceRange = (dragContent ? timeClip.SourceRange : timeClip.TimeRange);
        var scale = 1f;
        if (dragContent && timeClip.SourceRange.Duration != 0 && timeClip.SourceRange.Duration != 0)
            scale = timeClip.TimeRange.Duration / timeClip.SourceRange.Duration;

        if (attr.MoveClipsCommand == null)
        {
            var dragStartedAtTime = attr.LayerContext.TimeCanvas.InverseTransformX(mousePos.X);
            _timeWithinDraggedClip = dragStartedAtTime - referenceRange.Start;
            _posPosYOnDragStart = mousePos.Y;
            attr.LayerContext.TimeCanvas.StartDragCommand(attr.CompositionOp.Symbol.Id);
        }

        switch (mode)
        {
            case HandleDragMode.Body:
                var currentDragTime = attr.LayerContext.TimeCanvas.InverseTransformX(mousePos.X);

                var newStartTime = currentDragTime - _timeWithinDraggedClip;
                var dy = _posPosYOnDragStart - mousePos.Y;

                if (attr.LayerContext.SnapHandler.TryCheckForSnapping(newStartTime, out var snappedValue, attr.LayerContext.TimeCanvas.Scale.X * scale))
                {
                    newStartTime = (float)snappedValue;
                    attr.LayerContext.TimeCanvas.UpdateDragCommand(newStartTime - referenceRange.Start, dy);
                    return;
                }

                var newEndTime = newStartTime + referenceRange.Duration;
                if (attr.LayerContext.SnapHandler.TryCheckForSnapping(newEndTime, out var snappedValue2, attr.LayerContext.TimeCanvas.Scale.X * scale))
                {
                    newEndTime = (float)snappedValue2;
                }

                attr.LayerContext.TimeCanvas.UpdateDragCommand(newEndTime - referenceRange.End, dy);
                break;

            case HandleDragMode.Start:
                var newDragStartTime = attr.LayerContext.TimeCanvas.InverseTransformX(mousePos.X);
                if (attr.LayerContext.SnapHandler.TryCheckForSnapping(newDragStartTime, out var snappedValue3, attr.LayerContext.TimeCanvas.Scale.X * scale))
                {
                    newDragStartTime = (float)snappedValue3;
                }

                attr.LayerContext.TimeCanvas.UpdateDragAtStartPointCommand(newDragStartTime - timeClip.TimeRange.Start, 0);
                break;

            case HandleDragMode.End:
                var newDragTime = attr.LayerContext.TimeCanvas.InverseTransformX(mousePos.X);
                if (attr.LayerContext.SnapHandler.TryCheckForSnapping(newDragTime, out var snappedValue4, attr.LayerContext.TimeCanvas.Scale.X * scale))
                {
                    newDragTime = (float)snappedValue4;
                }

                attr.LayerContext.TimeCanvas.UpdateDragAtEndPointCommand(newDragTime - timeClip.TimeRange.End, 0);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    private const float HandleWidth = 7;
    private static float _timeWithinDraggedClip;
    private static readonly Vector2 _handleOffset = new(HandleWidth, 0);
    private static readonly Color _timeRemappingColor = UiColors.StatusAnimated.Fade(0.5f);
    private static float _posPosYOnDragStart;
}