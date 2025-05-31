#nullable enable
using T3.SystemUi;

namespace T3.Editor.Gui.Interaction;
internal sealed class KeyCombination(Key key, bool ctrl = false, bool alt = false, bool shift = false)
{
    public Key Key { get; } = key;
    public bool Ctrl { get; } = ctrl;
    public bool Alt { get; } = alt;
    public bool Shift { get; } = shift;

    public override string ToString()
    {
        var parts = new List<string>();
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        parts.Add(Key.ToString());

        return string.Join("+", parts);
    }
}