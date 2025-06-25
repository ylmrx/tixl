#nullable enable
using System.IO;
using T3.Core.UserData;

namespace T3.Editor.Gui.Interaction.Keyboard;

/// <summary>
/// Defines a complete set of <see cref="KeyBinding"/> 
/// </summary>
/// <remarks>Note: Some of these are public to enable serialization to json</remarks>
public sealed class KeyMap
{
    public List<KeyBinding> Bindings = [];

    public string Name = "untitled";
    public string Author = "unknown";
    
    private void InitializeShortcutLabels()
    {
        ShortcutLabels.Clear();

        foreach (var action in Enum.GetValues<UserActions>())
        {
            var shortcuts = Bindings
                           .Where(b => b.Action == action)
                           .Select(b => b.KeyCombination.ToString())
                           .ToList();

            if (shortcuts.Count > 0)
            {
                ShortcutLabels[action] = string.Join(" and ", shortcuts);
            }
        }
    }

    public readonly Dictionary<UserActions, string> ShortcutLabels = new();

    public KeyMap Clone()
    {
        var newKeyMap = new KeyMap
                            {
                                Bindings = [..Bindings],
                                Name = Name,
                                Author = Author
                            };

        return newKeyMap;
    }

    
    public void ClearKeyboardShortcut(UserActions action)
    {
        // FIXME: Implement
        
    }
    
    // FIXME: Implement
    public void SetKeyboardShortcut(UserActions action, string shortcut, string bindingName)
    {
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
    
    // private static void LoadBindingsFromJson()
    // {
    //     var folder = Path.Combine(FileLocations.ReadOnlySettingsPath, FileLocations.KeyBindingSubFolder);
    //     var jsonPath = Path.Combine(folder, "QWERTY.json");
    //
    //     try
    //     {
    //         if (!File.Exists(jsonPath))
    //         {
    //             CreateDefaultBindingsFile(jsonPath);
    //         }
    //
    //         var json = File.ReadAllText(jsonPath);
    //         // var options = new JsonSerializerOptions
    //         //                   {
    //         //                       PropertyNameCaseInsensitive = true,
    //         //                       ReadCommentHandling = JsonCommentHandling.Skip
    //         //                   };
    //
    //         var jsonBindings = JsonSerializer.Deserialize<KeyBinding>(json, options);
    //         ParseJsonBindings(jsonBindings);
    //     }
    //     catch
    //     {
    //         // Fall back to default bindings if loading fails
    //         InitializeDefaultBindings();
    //     }
    // }
    //
    // private static void ParseJsonBindings(KeyboardBindingsJson? jsonBindings)
    // {
    //     if (jsonBindings?.KeyboardBindings == null)
    //     {
    //         InitializeDefaultBindings();
    //         return;
    //     }
    //
    //     // Load metadata
    //     CurrentBindingSetName = jsonBindings.Name ?? "QWERTY";
    //     CurrentBindingSetAuthor = jsonBindings.Author ?? "Community";
    //
    //     _bindings.Clear();
    //
    //     foreach (var binding in jsonBindings.KeyboardBindings)
    //     {
    //         if (!Enum.TryParse<Key>(binding.Key, out var key) ||
    //             !Enum.TryParse<UserActions>(binding.Action, out var action))
    //             continue;
    //
    //         var combination = new KeyCombination(key,
    //                                              binding.Ctrl ?? false,
    //                                              binding.Alt ?? false,
    //                                              binding.Shift ?? false);
    //
    //         _bindings.Add(new KeyActionHandling(
    //                                             action,
    //                                             combination,
    //                                             binding.NeedsWindowFocus ?? false,
    //                                             binding.NeedsWindowHover ?? false,
    //                                             binding.KeyPressOnly ?? false,
    //                                             binding.KeyHoldOnly ?? false
    //                                            ));
    //     }
    // }
    //
    // private static void CreateDefaultBindingsFile(string path)
    // {
    //     InitializeDefaultBindings();
    //     SaveBindingsToFile(path);
    // }
}