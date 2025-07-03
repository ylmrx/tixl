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
            
    public void UpdateForComposition(Instance compositionOp)
    {
        _compositionOp = compositionOp;
        _compositionTimeClips.Clear();
                
        // Avoiding Linq for GC reasons 
        foreach (var child in compositionOp.Children.Values)
        {
            foreach (var output in child.Outputs)
            {
                if (output is ITimeClipProvider clipProvider)
                {
                    _compositionTimeClips[clipProvider.TimeClip.Id] = clipProvider.TimeClip;
                }
            }
        }
                
        _selectedClips.Clear();
        foreach (var selectedGraphNode in _nodeSelection.Selection)
        {
            if (_compositionTimeClips.TryGetValue(selectedGraphNode.Id, out var selectedTimeClip))
            {
                _selectedClips.Add(selectedTimeClip);
            }
        }
    }

    public List<ITimeClip> SelectedClips => _selectedClips;
    public int Count => _selectedClips.Count;
    public IReadOnlyCollection<ITimeClip> AllOrSelectedClips => _selectedClips.Count > 0 ? _selectedClips : AllClips;

    public IReadOnlyCollection<ITimeClip> AllClips => _compositionTimeClips.Values;

    public void Clear()
    {
        if (_compositionOp == null) 
            return;
            
        foreach (var c in _selectedClips)
        {
            _nodeSelection.DeselectCompositionChild(_compositionOp, c.Id);
        }
                
        _selectedClips.Clear();
    }

    public void Select(ITimeClip timeClip)
    {
        if (_compositionOp == null) 
            return;
            
        foreach (var c in _selectedClips)
        {
            _nodeSelection.DeselectCompositionChild(_compositionOp, c.Id);
        }
        _nodeSelection.SelectCompositionChild(_compositionOp, timeClip.Id);
        _selectedClips.Add(timeClip);
    }

    public void Deselect(ITimeClip timeClip)
    {
        if (_compositionOp == null) 
            return;

        _nodeSelection.DeselectCompositionChild(_compositionOp, timeClip.Id);
        _selectedClips.Remove(timeClip);
    }

    public void AddSelection(ITimeClip matchingClip)
    {
        if (_compositionOp == null) 
            return;

        _nodeSelection.SelectCompositionChild(_compositionOp, matchingClip.Id);
        _selectedClips.Add(matchingClip);
    }
            


    public bool Contains(ITimeClip clip)
    {
        return _selectedClips.Contains(clip);
    }
            
    /// <summary>
    /// Reusing static collections to avoid GC leaks
    /// </summary>
    private readonly Dictionary<Guid, ITimeClip> _compositionTimeClips = new(100);
    private readonly List<ITimeClip> _selectedClips = new(100);
    private Instance? _compositionOp;
    private readonly NodeSelection _nodeSelection;
}