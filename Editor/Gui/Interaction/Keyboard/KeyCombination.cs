#nullable enable
using ImGuiNET;
using T3.SystemUi;

namespace T3.Editor.Gui.Interaction.Keyboard;
/// <summary>
/// Defines a combination that can be saved with a keymap
/// </summary>
internal readonly struct KeyCombination(Key key, bool ctrl = false, bool alt = false, bool shift = false)
{
    internal Key Key { get; } = key;
    private bool Ctrl { get; } = ctrl;
    private bool Alt { get; } = alt;
    private bool Shift { get; } = shift;

    
    internal bool ModifiersMatch(ImGuiIOPtr io)
    {
        return (!Alt || io.KeyAlt) && (Alt || !io.KeyAlt)
                                               && (!Ctrl || io.KeyCtrl) && (Ctrl || !io.KeyCtrl)
                                               && (!Shift || io.KeyShift) && (Shift || !io.KeyShift);
    }    
    
    // TODO: Refactor into try pattern
    internal static KeyCombination? ParseShortcutString(string shortcut)
    {
        try
        {
            var parts = shortcut.Split('+');
            
            var key = Enum.Parse<Key>(parts.Last());
            var ctrl = parts.Contains("Ctrl");
            var alt = parts.Contains("Alt");
            var shift = parts.Contains("Shift");

            return new KeyCombination(key, ctrl, alt, shift);
        }
        catch
        {
            return null;
        }
    }
    
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