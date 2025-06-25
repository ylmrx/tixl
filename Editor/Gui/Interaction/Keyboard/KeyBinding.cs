#nullable enable
using ImGuiNET;
using Newtonsoft.Json;
using T3.Serialization;

namespace T3.Editor.Gui.Interaction.Keyboard;

/// <summary>
/// Controls the assignment of <see cref="Keyboard.KeyCombination"/> to <see cref="UserActions"/>
/// </summary>
public sealed class KeyBinding
{
    internal KeyBinding(UserActions action, KeyCombination combination,
                        bool needsWindowFocus = false,
                        bool needsWindowHover = false,
                        bool keyPressOnly = false,
                        bool keyHoldOnly = false)
    {
        Action = action;
        KeyCombination = combination;
        NeedsWindowFocus = needsWindowFocus;
        NeedsWindowHover = needsWindowHover;
        KeyPressOnly = keyPressOnly;
        KeyHoldOnly = keyHoldOnly;
    }
    
    [JsonConverter(typeof(SafeEnumConverter<UserActions>))]
    public UserActions Action { get; }
    
    
    public KeyCombination KeyCombination { get; }
    
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool NeedsWindowFocus { get; }
    
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool NeedsWindowHover { get; }
    
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool KeyPressOnly { get; }
    
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool KeyHoldOnly { get; } //

    
    public bool IsContextValid()
    {
        if (NeedsWindowFocus && !ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows))
            return false;

        if (NeedsWindowHover && !ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows))
            return false;

        return true;
    }

    public bool IsTriggered(ImGuiIOPtr io)
    {
        if (!KeyCombination.ModifiersMatch(io))
            return false;

        if (KeyHoldOnly)
            return ImGui.IsKeyDown((ImGuiKey)KeyCombination.Key);

        if (KeyPressOnly)
            return ImGui.IsKeyPressed((ImGuiKey)KeyCombination.Key, false);

        // Default behavior (works for both press and hold)
        return ImGui.IsKeyPressed((ImGuiKey)KeyCombination.Key, true);
    }
}