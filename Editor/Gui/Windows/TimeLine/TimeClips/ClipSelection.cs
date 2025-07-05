#nullable enable
using T3.Core.Animation;
using T3.Core.Operator;
using T3.Core.Operator.Slots;
using T3.Editor.UiModel.Selection;

namespace T3.Editor.Gui.Windows.TimeLine.TimeClips;

/// <summary>
/// Maps selection of <see cref="ITimeClip"/>s
/// to <see cref="NodeSelection"/> with <see cref="ISelectableCanvasObject"/>s.
/// </summary>
internal sealed class ClipSelection
{
    public ClipSelection(NodeSelection nodeSelection)
    {
        _nodeSelection = nodeSelection;
    }
 
    public List<Guid> SelectedClipsIds { get; } = new(100);

    public int Count => SelectedClipsIds.Count;
    public IReadOnlyCollection<Guid> AllOrSelectedClipIds => SelectedClipsIds.Count > 0 ? SelectedClipsIds : AllClipIds;
    public IReadOnlyCollection<Guid> AllClipIds => CompositionTimeClips.Keys;

    public IEnumerable<ITimeClip> GetAllOrSelectedClips()
    {
        if (SelectedClipsIds.Count == 0)
        {
            foreach (var clip in CompositionTimeClips.Values)
            {
                yield return clip;
                
            }
            
            yield break;
        }

        foreach (var id in SelectedClipsIds)
        {
            yield return CompositionTimeClips[id];
        }
    }
    
    public void UpdateForComposition(Instance compositionOp)
    {
        _compositionOp = compositionOp;
        CompositionTimeClips.Clear();
                
        // Avoiding Linq for GC reasons 
        foreach (var child in compositionOp.Children.Values)
        {
            foreach (var output in child.Outputs)
            {
                if (output is ITimeClipProvider clipProvider)
                {
                    CompositionTimeClips[clipProvider.TimeClip.Id] = clipProvider.TimeClip;
                }
            }
        }
                
        SelectedClipsIds.Clear();
        foreach (var selectedGraphNode in _nodeSelection.Selection)
        {
            if (CompositionTimeClips.TryGetValue(selectedGraphNode.Id, out var selectedTimeClip))
            {
                SelectedClipsIds.Add(selectedTimeClip.Id);
            }
        }
    }

    public void Clear()
    {
        if (_compositionOp == null) 
            return;
            
        foreach (var id in SelectedClipsIds)
        {
            _nodeSelection.DeselectCompositionChild(_compositionOp, id);
        }
                
        SelectedClipsIds.Clear();
    }

    public void Select(ITimeClip timeClip)
    {
        if (_compositionOp == null) 
            return;
            
        foreach (var id in SelectedClipsIds)
        {
            _nodeSelection.DeselectCompositionChild(_compositionOp, id);
        }
        _nodeSelection.SelectCompositionChild(_compositionOp, timeClip.Id);
        SelectedClipsIds.Add(timeClip.Id);
    }

    public void Deselect(ITimeClip timeClip)
    {
        if (_compositionOp == null) 
            return;

        _nodeSelection.DeselectCompositionChild(_compositionOp, timeClip.Id);
        SelectedClipsIds.Remove(timeClip.Id);
    }

    public void AddSelection(ITimeClip matchingClip)
    {
        if (_compositionOp == null) 
            return;

        _nodeSelection.SelectCompositionChild(_compositionOp, matchingClip.Id);
        SelectedClipsIds.Add(matchingClip.Id);
    }
            


    public bool Contains(ITimeClip clip)
    {
        return SelectedClipsIds.Contains(clip.Id);
    }
            
    /// <summary>
    /// Reusing static collections to avoid GC leaks
    /// </summary>
    internal readonly Dictionary<Guid, ITimeClip> CompositionTimeClips = new(100);

    private Instance? _compositionOp;
    private readonly NodeSelection _nodeSelection;
}