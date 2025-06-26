#nullable enable
namespace T3.Editor.Gui.Interaction.Keyboard;

/// <summary>
/// Defines a complete set of <see cref="KeyBinding"/> 
/// </summary>
/// <remarks>Note: Some of these are public to enable serialization to json</remarks>
internal sealed class KeyMap
{
    internal List<KeyBinding> Bindings = [];

    internal string Name = "untitled";
    internal string Author = "unknown";
    
    /** Prevent factory setting from being modified */
    internal bool IsLocked;

    internal void UpdateShortcutLabels()
    {
        for (var index = 0; index < ShortCutsLabelsForActions.Length; index++)
        {
            ShortCutsLabelsForActions[index] = string.Empty;
        }

        foreach (var action in Enum.GetValues<UserActions>())
        {
            var shortcuts = Bindings
                           .Where(b => b.Action == action)
                           .Select(b => b.KeyCombination.ToString())
                           .ToList();

            if (shortcuts.Count > 0)
            {
                ShortCutsLabelsForActions[(int)action] = string.Join(" and ", shortcuts);
            }
        }
    }

    internal readonly string[] ShortCutsLabelsForActions = new string[(int)UserActions.__Count];

    internal KeyMap Clone()
    {
        var newKeyMap = new KeyMap
                            {
                                Bindings = [..Bindings],
                                Name = Name,
                                Author = Author
                            };

        return newKeyMap;
    }

    internal void ClearKeyboardShortcut(UserActions action)
    {
        // TODO: Implement
        Log.Warning("Clearing not implemented yet");
    }

    public void SetKeyboardShortcut(UserActions action, string shortcut, string bindingName)
    {
        // TODO: Implement
        Log.Warning("Clearing not implemented yet");
        // try
        // {
        //     var folder = Path.Combine(FileLocations.SettingsPath, FileLocations.KeyBindingSubFolder);
        //     var filePath = Path.Combine(folder, bindingName + ".json");
        //
        //     if (!File.Exists(filePath))
        //     {
        //         // Create a new binding file if it doesn't exist
        //         var newBindings = new KeyBinding()
        //                               {
        //                                   Name = bindingName,
        //                                   Author = "User",
        //                                   KeyboardBindings = _bindings.Select(b => new KeyboardBindingJson
        //                                                                                {
        //                                                                                    Action = b.Action.ToString(),
        //                                                                                    Key = b.KeyCombination.Key.ToString(),
        //                                                                                    Ctrl = b.KeyCombination.Ctrl,
        //                                                                                    Alt = b.KeyCombination.Alt,
        //                                                                                    Shift = b.KeyCombination.Shift,
        //                                                                                    NeedsWindowFocus = b.NeedsWindowFocus,
        //                                                                                    NeedsWindowHover = b.NeedsWindowHover,
        //                                                                                    KeyPressOnly = b.KeyPressOnly,
        //                                                                                    KeyHoldOnly = b.KeyHoldOnly
        //                                                                                }).ToList()
        //                               };
        //
        //         File.WriteAllText(filePath, JsonSerializer.Serialize(newBindings, JsonOptions));
        //     }
        //
        //     // Load the existing bindings
        //     var json = File.ReadAllText(filePath);
        //     var bindings = JsonSerializer.Deserialize<KeyboardBindingsJson>(json, JsonOptions);
        //
        //     // Update the specific action's shortcut
        //     if (!string.IsNullOrEmpty(shortcut))
        //     {
        //         var combination = ParseShortcutString(shortcut);
        //         if (combination != null)
        //         {
        //             var existing = bindings.KeyboardBindings.FirstOrDefault(b => b.Action == action.ToString());
        //             if (existing != null)
        //             {
        //                 existing.Key = combination.Key.ToString();
        //                 existing.Ctrl = combination.Ctrl;
        //                 existing.Alt = combination.Alt;
        //                 existing.Shift = combination.Shift;
        //             }
        //             else
        //             {
        //                 bindings.KeyboardBindings.Add(new KeyboardBindingJson
        //                                                   {
        //                                                       Action = action.ToString(),
        //                                                       Key = combination.Key.ToString(),
        //                                                       Ctrl = combination.Ctrl,
        //                                                       Alt = combination.Alt,
        //                                                       Shift = combination.Shift
        //                                                   });
        //             }
        //         }
        //     }
        //     else
        //     {
        //         // Remove the binding if shortcut is empty
        //         bindings.KeyboardBindings.RemoveAll(b => b.Action == action.ToString());
        //     }
        //
        //     // Save the changes
        //     File.WriteAllText(filePath, JsonSerializer.Serialize(bindings, JsonOptions));
        //
        //     // Reload the bindings
        //     LoadCustomBindings(bindingName + ".json");
        // }
        // catch (Exception e)
        // {
        //     Log.Error($"Failed to update shortcut: {e.Message}");
        // }
    }
}