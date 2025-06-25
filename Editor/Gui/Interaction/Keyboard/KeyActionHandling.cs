#nullable enable
using ImGuiNET;
using T3.Editor.Gui.UiHelpers;
using T3.SystemUi;

namespace T3.Editor.Gui.Interaction.Keyboard;

/// <summary>
/// A static class that helps to trigger user actions with assigned keyboard shortcuts  
/// </summary>
internal static class KeyActionHandling
{
    //private static readonly List<KeyBinding> _bindings = [];

    private static bool _initialized;
    private static bool _anyKeysPressed;
    
    internal static void InitializeFrame()
    {
        if (!_initialized)
        {
            _initialized = true;
        }
    
        _anyKeysPressed = ImGui.GetIO().KeysDown.Count > 0;
    }
    
    
    internal static bool Triggered(this UserActions action)
    {
        if (!_anyKeysPressed || !UserSettings.Config.EnableKeyboardShortCuts)
            return false;

        // Prevent keyboard shortcuts while inputs are active
        if (ImGui.IsAnyItemActive())
            return false;

        var io = ImGui.GetIO();

        foreach (var binding in KeyMapSwitching.CurrentKeymap.Bindings)
        {
            if (binding.Action != action)
                continue;

            if (!binding.IsContextValid())
                continue;

            if (binding.IsTriggered(io))
                return true;
        }

        return false;
    }

    internal static string ListKeyboardShortcutsForAction(this UserActions action, bool showLabel = true)
    {
        if (!KeyMapSwitching.CurrentKeymap.ShortcutLabels.TryGetValue(action, out var shortcuts))
            return string.Empty;

        if (!showLabel)
            return shortcuts;

        var label = shortcuts.Contains(" and ") ? "Shortcuts: " : "Shortcut: ";
        return label + shortcuts;
    }

    /// <summary>
    /// Saves the current bindings to a custom file with specified name and author
    /// </summary>
    // public static void SaveCustomBindings(string fileName, string name, string author)
    // {
    //     CurrentBindingSetName = name;
    //     CurrentBindingSetAuthor = author;
    //
    //     var folder = Path.Combine(FileLocations.SettingsPath, FileLocations.KeyBindingSubFolder);
    //     var customPath = Path.Combine(folder, fileName.EndsWith(".json") ? fileName : fileName + ".json");
    //
    //     SaveBindingsToFile(customPath);
    // }

    // public static bool LoadCustomBindings(string fileName)
    // {
    //     try
    //     {
    //         var folder = Path.Combine(FileLocations.SettingsPath, FileLocations.KeyBindingSubFolder);
    //         var customPath = Path.Combine(folder, fileName.EndsWith(".json") ? fileName : fileName + ".json");
    //         if (!File.Exists(customPath))
    //             return false;
    //         var json = File.ReadAllText(customPath);
    //         // Use the shared options
    //         var jsonBindings = JsonSerializer.Deserialize<KeyboardBindingsJson>(json, JsonOptions);
    //         ParseJsonBindings(jsonBindings);
    //         InitializeShortcutLabels();
    //         return true;
    //     }
    //     catch
    //     {
    //         return false;
    //     }
    // }
    //
    // private static void SaveBindingsToFile(string path)
    // {
    //     var jsonBindings = new KeyboardBindingsJson
    //     {
    //         Name = CurrentBindingSetName,
    //         Author = CurrentBindingSetAuthor,
    //         KeyboardBindings = [.. _bindings.Select(b => new KeyboardBindingJson
    //     {
    //         Action = b.Action.ToString(),
    //         Key = b.Combination.Key.ToString(),
    //         Ctrl = b.Combination.Ctrl ? true : null,
    //         Alt = b.Combination.Alt ? true : null,
    //         Shift = b.Combination.Shift ? true : null,
    //         NeedsWindowFocus = b.NeedsWindowFocus ? true : null,
    //         NeedsWindowHover = b.NeedsWindowHover ? true : null,
    //         KeyPressOnly = b.KeyPressOnly ? true : null,
    //         KeyHoldOnly = b.KeyHoldOnly ? true : null
    //     })]
    //     };
    //
    //     var directory = Path.GetDirectoryName(path);
    //     if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
    //     {
    //         Directory.CreateDirectory(directory);
    //         Thread.Sleep(100); // Ensure directory is created before writing
    //     }
    //
    //     // Use the same shared options
    //     File.WriteAllText(path, JsonSerializer.Serialize(jsonBindings, JsonOptions));
    // }

    // private static void InitializeDefaultBindings()
    // {
    //     _bindings.Clear();
    //     _bindings.AddRange(CreateDefaultBindings());
    // }



    // #region JSON Classes
    // private class KeyboardBindingJson
    // {
    //     public string Action { get; set; } = string.Empty;
    //     public string Key { get; set; } = string.Empty;
    //     public bool? Ctrl { get; set; }
    //     public bool? Alt { get; set; }
    //     public bool? Shift { get; set; }
    //     public bool? NeedsWindowFocus { get; set; }
    //     public bool? NeedsWindowHover { get; set; }
    //     public bool? KeyPressOnly { get; set; }
    //     public bool? KeyHoldOnly { get; set; }
    // }
    //
    // private class KeyboardBindingsJson
    // {
    //     public string Name { get; set; } = "QWERTY";
    //     public string Author { get; set; } = "Community";
    //     public List<KeyboardBindingJson> KeyboardBindings { get; set; } = new();
    // }
    // #endregion


}