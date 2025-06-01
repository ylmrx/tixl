#nullable enable
using ImGuiNET;
using System.IO;
using System.Text.Json;
using System.Threading;
using T3.Core.UserData;
using T3.Editor.Gui.UiHelpers;
using T3.SystemUi;

namespace T3.Editor.Gui.Interaction;

internal sealed class KeyboardBinding
{
    private static readonly List<KeyboardBinding> _bindings = new();
    private static readonly Dictionary<UserActions, string> _shortcutLabels = new();
    private static bool _initialized;
    private static bool _anyKeysPressed;

    // Metadata for the current binding set
    public static string CurrentBindingSetName { get; private set; } = "QWERTY";
    public static string CurrentBindingSetAuthor { get; private set; } = "Community";

    public UserActions Action { get; }
    public KeyCombination Combination { get; }
    public bool NeedsWindowFocus { get; }
    public bool NeedsWindowHover { get; }
    public bool KeyPressOnly { get; }
    public bool KeyHoldOnly { get; }  // 

    private KeyboardBinding(UserActions action, KeyCombination combination,
        bool needsWindowFocus = false,
        bool needsWindowHover = false,
        bool keyPressOnly = false,
        bool keyHoldOnly = false)
    {
        Action = action;
        Combination = combination;
        NeedsWindowFocus = needsWindowFocus;
        NeedsWindowHover = needsWindowHover;
        KeyPressOnly = keyPressOnly;
        KeyHoldOnly = keyHoldOnly;
    }

    internal static void Initialize()
    {
        if (!_initialized)
        {
            LoadBindingsFromJson();
            InitializeShortcutLabels();
            _initialized = true;
        }

        _anyKeysPressed = ImGui.GetIO().KeysDown.Count > 0;
    }

    internal static bool Triggered(UserActions action)
    {
        if (!_anyKeysPressed || !UserSettings.Config.EnableKeyboardShortCuts)
            return false;

        // Prevent keyboard shortcuts while inputs are active
        if (ImGui.IsAnyItemActive())
            return false;

        var io = ImGui.GetIO();

        foreach (var binding in _bindings)
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

    private bool IsContextValid()
    {
        if (NeedsWindowFocus && !ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows))
            return false;

        if (NeedsWindowHover && !ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows))
            return false;

        return true;
    }

    private bool IsTriggered(ImGuiIOPtr io)
    {
        if (!ModifiersMatch(io))
            return false;

        if (KeyHoldOnly)
            return ImGui.IsKeyDown((ImGuiKey)Combination.Key);

        if (KeyPressOnly)
            return ImGui.IsKeyPressed((ImGuiKey)Combination.Key, false);

        // Default behavior (works for both press and hold)
        return ImGui.IsKeyPressed((ImGuiKey)Combination.Key, true);
    }

    private bool ModifiersMatch(ImGuiIOPtr io)
    {
        return (!Combination.Alt || io.KeyAlt) && (Combination.Alt || !io.KeyAlt)
               && (!Combination.Ctrl || io.KeyCtrl) && (Combination.Ctrl || !io.KeyCtrl)
               && (!Combination.Shift || io.KeyShift) && (Combination.Shift || !io.KeyShift);
    }

    internal static string ListKeyboardShortcuts(UserActions action, bool showLabel = true)
    {
        if (!_shortcutLabels.TryGetValue(action, out var shortcuts))
            return string.Empty;

        if (!showLabel)
            return shortcuts;

        var label = shortcuts.Contains(" and ") ? "Shortcuts: " : "Shortcut: ";
        return label + shortcuts;
    }

    private static void LoadBindingsFromJson()
    {
        var folder = Path.Combine(FileLocations.ReadOnlySettingsPath, FileLocations.KeyBindingSubFolder);
        var jsonPath = Path.Combine(folder, "QWERTY.json");

        try
        {
            if (!File.Exists(jsonPath))
            {
                CreateDefaultBindingsFile(jsonPath);
            }

            var json = File.ReadAllText(jsonPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var jsonBindings = JsonSerializer.Deserialize<KeyboardBindingsJson>(json, options);
            ParseJsonBindings(jsonBindings);
        }
        catch
        {
            // Fall back to default bindings if loading fails
            InitializeDefaultBindings();
        }
    }

    private static void ParseJsonBindings(KeyboardBindingsJson? jsonBindings)
    {
        if (jsonBindings?.KeyboardBindings == null)
        {
            InitializeDefaultBindings();
            return;
        }

        // Load metadata
        CurrentBindingSetName = jsonBindings.Name ?? "QWERTY";
        CurrentBindingSetAuthor = jsonBindings.Author ?? "Community";

        _bindings.Clear();

        foreach (var binding in jsonBindings.KeyboardBindings)
        {
            if (!Enum.TryParse<Key>(binding.Key, out var key) ||
                !Enum.TryParse<UserActions>(binding.Action, out var action))
                continue;

            var combination = new KeyCombination(key,
                binding.Ctrl ?? false,
                binding.Alt ?? false,
                binding.Shift ?? false);

            _bindings.Add(new KeyboardBinding(
                action,
                combination,
                binding.NeedsWindowFocus ?? false,
                binding.NeedsWindowHover ?? false,
                binding.KeyPressOnly ?? false,
                binding.KeyHoldOnly ?? false
            ));
        }
    }

    private static void CreateDefaultBindingsFile(string path)
    {
        InitializeDefaultBindings();
        SaveBindingsToFile(path);
    }

    /// <summary>
    /// Saves the current bindings to a custom file with specified name and author
    /// </summary>
    public static void SaveCustomBindings(string fileName, string name, string author)
    {
        CurrentBindingSetName = name;
        CurrentBindingSetAuthor = author;
        
        var folder = Path.Combine(FileLocations.SettingsPath, FileLocations.KeyBindingSubFolder);
        var customPath = Path.Combine(folder, fileName.EndsWith(".json") ? fileName : fileName + ".json");

        SaveBindingsToFile(customPath);
    }

    /// <summary>
    /// Loads bindings from a custom file
    /// </summary> 
    /// 
    // Single options object that works for both reading and writing
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static bool LoadCustomBindings(string fileName)
    {
        try
        {
            var folder = Path.Combine(FileLocations.SettingsPath, FileLocations.KeyBindingSubFolder);
            var customPath = Path.Combine(folder, fileName.EndsWith(".json") ? fileName : fileName + ".json");
            if (!File.Exists(customPath))
                return false;
            var json = File.ReadAllText(customPath);
            // Use the shared options
            var jsonBindings = JsonSerializer.Deserialize<KeyboardBindingsJson>(json, JsonOptions);
            ParseJsonBindings(jsonBindings);
            InitializeShortcutLabels();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SaveBindingsToFile(string path)
    {
        var jsonBindings = new KeyboardBindingsJson
        {
            Name = CurrentBindingSetName,
            Author = CurrentBindingSetAuthor,
            KeyboardBindings = [.. _bindings.Select(b => new KeyboardBindingJson
        {
            Action = b.Action.ToString(),
            Key = b.Combination.Key.ToString(),
            Ctrl = b.Combination.Ctrl ? true : null,
            Alt = b.Combination.Alt ? true : null,
            Shift = b.Combination.Shift ? true : null,
            NeedsWindowFocus = b.NeedsWindowFocus ? true : null,
            NeedsWindowHover = b.NeedsWindowHover ? true : null,
            KeyPressOnly = b.KeyPressOnly ? true : null,
            KeyHoldOnly = b.KeyHoldOnly ? true : null
        })]
        };

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Thread.Sleep(100); // Ensure directory is created before writing
        }

        // Use the same shared options
        File.WriteAllText(path, JsonSerializer.Serialize(jsonBindings, JsonOptions));
    }

    private static void InitializeDefaultBindings()
    {
        _bindings.Clear();
        _bindings.AddRange(CreateDefaultBindings());
    }

    private static IEnumerable<KeyboardBinding> CreateDefaultBindings()
    {
        // General actions
        yield return new(UserActions.Save, new(Key.S, ctrl: true), keyPressOnly: true);
        yield return new(UserActions.FocusSelection, new(Key.F), needsWindowHover: true);
        yield return new(UserActions.Duplicate, new(Key.D, ctrl: true), needsWindowFocus: true);
        yield return new(UserActions.DeleteSelection, new(Key.Delete), needsWindowFocus: true);
        yield return new(UserActions.DeleteSelection, new(Key.Backspace), needsWindowFocus: true);
        yield return new(UserActions.CopyToClipboard, new(Key.C, ctrl: true), needsWindowFocus: true);
        yield return new(UserActions.PasteFromClipboard, new(Key.V, ctrl: true), needsWindowFocus: true);
        yield return new(UserActions.PasteValues, new(Key.V, ctrl: true, shift: true), needsWindowFocus: true);
        yield return new(UserActions.Undo, new(Key.Z, ctrl: true), keyPressOnly: true);
        yield return new(UserActions.Redo, new(Key.Z, ctrl: true, shift: true), keyPressOnly: true);

        // Playback controls
        yield return new(UserActions.PlaybackForward, new(Key.L), keyPressOnly: true);
        yield return new(UserActions.PlaybackForwardHalfSpeed, new(Key.L, shift: true), keyPressOnly: true);
        yield return new(UserActions.PlaybackBackwards, new(Key.J), keyPressOnly: true);
        yield return new(UserActions.PlaybackStop, new(Key.K), keyPressOnly: true);
        yield return new(UserActions.PlaybackToggle, new(Key.Space), keyPressOnly: true);
        yield return new(UserActions.PlaybackPreviousFrame, new(Key.CursorLeft, shift: true));
        yield return new(UserActions.PlaybackNextFrame, new(Key.CursorRight, shift: true));
        yield return new(UserActions.PlaybackJumpToStartTime, new(Key.Home), keyPressOnly: true);
        yield return new(UserActions.PlaybackJumpToNextKeyframe, new(Key.Period));
        yield return new(UserActions.PlaybackJumpToPreviousKeyframe, new(Key.Comma));

        // Timeline actions
        yield return new(UserActions.InsertKeyframe, new(Key.C), needsWindowFocus: true, keyPressOnly: true);
        yield return new(UserActions.InsertKeyframeWithIncrement, new(Key.C, shift: true));
        yield return new(UserActions.ToggleAnimationPinning, new(Key.K, shift: true));
        yield return new(UserActions.SetStartTime, new(Key.B), keyPressOnly: true);
        yield return new(UserActions.SetEndTime, new(Key.N), keyPressOnly: true);
        yield return new(UserActions.TapBeatSync, new(Key.Z), keyPressOnly:true);
        yield return new(UserActions.TapBeatSyncMeasure, new(Key.X), keyPressOnly: true);

        // Graph window actions
        yield return new(UserActions.ToggleDisabled, new(Key.D, shift: true), needsWindowFocus: true);
        yield return new(UserActions.ToggleBypassed, new(Key.B, shift: true), needsWindowFocus: true);
        yield return new(UserActions.PinToOutputWindow, new(Key.P), keyPressOnly: true);//, needsWindowFocus: true);
        yield return new(UserActions.DisplayImageAsBackground, new(Key.P, ctrl: true));
        yield return new(UserActions.ClearBackgroundImage, new(Key.P, ctrl: true, shift: true), needsWindowFocus: true);
        yield return new(UserActions.LayoutSelection, new(Key.G));
        yield return new(UserActions.AddAnnotation, new(Key.A, shift: true), needsWindowFocus: true, keyPressOnly: true);
        yield return new(UserActions.AddComment, new(Key.C, shift: true, ctrl: true), needsWindowFocus: true);
        yield return new(UserActions.ToggleVariationsWindow, new(Key.V, alt: true));
        yield return new(UserActions.SearchGraph, new(Key.F, ctrl: true));
        yield return new(UserActions.OpenOperator, new(Key.I), needsWindowFocus: true);
        yield return new(UserActions.CloseOperator, new(Key.U), needsWindowFocus: true);
        yield return new(UserActions.RenameChild, new(Key.Return), needsWindowFocus: true);

        // Navigation
        yield return new(UserActions.NavigateBackwards, new(Key.CursorLeft, alt: true));
        yield return new(UserActions.NavigateForward, new(Key.CursorRight, alt: true));
        yield return new(UserActions.SelectToAbove, new(Key.CursorUp), needsWindowFocus: true);
        yield return new(UserActions.SelectToRight, new(Key.CursorRight), needsWindowFocus: true);
        yield return new(UserActions.SelectToBelow, new(Key.CursorDown), needsWindowFocus: true);
        yield return new(UserActions.SelectToLeft, new(Key.CursorLeft), needsWindowFocus: true);

        // Camera controls
        yield return new(UserActions.CameraLeft, new(Key.A), needsWindowHover: true, keyHoldOnly: true);
        yield return new(UserActions.CameraRight, new(Key.D), needsWindowHover: true, keyHoldOnly: true);
        yield return new(UserActions.CameraForward, new(Key.W), needsWindowHover: true, keyHoldOnly: true);
        yield return new(UserActions.CameraBackward, new(Key.S), needsWindowHover: true, keyHoldOnly: true);
        yield return new(UserActions.CameraUp, new(Key.E), needsWindowHover: true, keyHoldOnly: true);
        yield return new(UserActions.CameraDown, new(Key.Q), needsWindowHover: true, keyHoldOnly: true);
        // Camera reset and focus
        yield return new(UserActions.CameraReset, new(Key.F), needsWindowHover: true);
        yield return new(UserActions.CameraFocusSelection, new(Key.C), needsWindowHover: true);

        // Layout and window management
        yield return new(UserActions.ToggleAllUiElements, new(Key.Esc, shift: true));
        yield return new(UserActions.ToggleFullscreen, new(Key.F11));
        yield return new(UserActions.ToggleFocusMode, new(Key.F12));

        // Generate bookmark bindings
        foreach (var binding in GenerateNumberedBindings(UserActions.LoadBookmark0, UserActions.SaveBookmark0, true))
            yield return binding;

        // Generate layout bindings  
        foreach (var binding in GenerateLayoutBindings())
            yield return binding;
    }

    private static IEnumerable<KeyboardBinding> GenerateNumberedBindings(UserActions loadBase, UserActions saveBase, bool useCtrl)
    {
        var keys = new[] { Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9 };

        for (int i = 0; i < 10; i++)
        {
            var loadAction = loadBase + i;
            var saveAction = saveBase + i;

            if (useCtrl)
            {
                yield return new(loadAction, new(keys[i], ctrl: true));
                yield return new(saveAction, new(keys[i], ctrl: true, shift: true));
            }
            else
            {
                yield return new(loadAction, new(keys[i]));
                yield return new(saveAction, new(keys[i], shift: true));
            }
        }
    }

    private static IEnumerable<KeyboardBinding> GenerateLayoutBindings()
    {
        var fKeys = new[] { Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10 };

        for (int i = 0; i < 10; i++)
        {
            yield return new(UserActions.LoadLayout0 + i, new(fKeys[i]));
            yield return new(UserActions.SaveLayout0 + i, new(fKeys[i], ctrl: true));
        }
    }

    private static void InitializeShortcutLabels()
    {
        _shortcutLabels.Clear();

        foreach (var action in Enum.GetValues<UserActions>())
        {
            var shortcuts = _bindings
                .Where(b => b.Action == action)
                .Select(b => b.Combination.ToString())
                .ToList();

            if (shortcuts.Count > 0)
            {
                _shortcutLabels[action] = string.Join(" and ", shortcuts);
            }
        }
    }

    #region JSON Classes
    private class KeyboardBindingJson
    {
        public string Action { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public bool? Ctrl { get; set; }
        public bool? Alt { get; set; }
        public bool? Shift { get; set; }
        public bool? NeedsWindowFocus { get; set; }
        public bool? NeedsWindowHover { get; set; }
        public bool? KeyPressOnly { get; set; }
        public bool? KeyHoldOnly { get; set; }
    }

    private class KeyboardBindingsJson
    {
        public string Name { get; set; } = "QWERTY";
        public string Author { get; set; } = "Community";
        public List<KeyboardBindingJson> KeyboardBindings { get; set; } = new();
    }
    #endregion

    public static void SetKeyboardShortcut(UserActions action, string shortcut, string bindingName)
    {
        try
        {
            var folder = Path.Combine(FileLocations.SettingsPath, FileLocations.KeyBindingSubFolder);
            var filePath = Path.Combine(folder, bindingName + ".json");

            if (!File.Exists(filePath))
            {
                // Create a new binding file if it doesn't exist
                var newBindings = new KeyboardBindingsJson
                {
                    Name = bindingName,
                    Author = "User",
                    KeyboardBindings = _bindings.Select(b => new KeyboardBindingJson
                    {
                        Action = b.Action.ToString(),
                        Key = b.Combination.Key.ToString(),
                        Ctrl = b.Combination.Ctrl,
                        Alt = b.Combination.Alt,
                        Shift = b.Combination.Shift,
                        NeedsWindowFocus = b.NeedsWindowFocus,
                        NeedsWindowHover = b.NeedsWindowHover,
                        KeyPressOnly = b.KeyPressOnly,
                        KeyHoldOnly = b.KeyHoldOnly
                    }).ToList()
                };

                File.WriteAllText(filePath, JsonSerializer.Serialize(newBindings, JsonOptions));
            }

            // Load the existing bindings
            var json = File.ReadAllText(filePath);
            var bindings = JsonSerializer.Deserialize<KeyboardBindingsJson>(json, JsonOptions);

            // Update the specific action's shortcut
            if (!string.IsNullOrEmpty(shortcut))
            {
                var combination = ParseShortcutString(shortcut);
                if (combination != null)
                {
                    var existing = bindings.KeyboardBindings.FirstOrDefault(b => b.Action == action.ToString());
                    if (existing != null)
                    {
                        existing.Key = combination.Key.ToString();
                        existing.Ctrl = combination.Ctrl;
                        existing.Alt = combination.Alt;
                        existing.Shift = combination.Shift;
                    }
                    else
                    {
                        bindings.KeyboardBindings.Add(new KeyboardBindingJson
                        {
                            Action = action.ToString(),
                            Key = combination.Key.ToString(),
                            Ctrl = combination.Ctrl,
                            Alt = combination.Alt,
                            Shift = combination.Shift
                        });
                    }
                }
            }
            else
            {
                // Remove the binding if shortcut is empty
                bindings.KeyboardBindings.RemoveAll(b => b.Action == action.ToString());
            }

            // Save the changes
            File.WriteAllText(filePath, JsonSerializer.Serialize(bindings, JsonOptions));

            // Reload the bindings
            LoadCustomBindings(bindingName + ".json");
        }
        catch (Exception e)
        {
            Log.Error($"Failed to update shortcut: {e.Message}");
        }
    }

    private static KeyCombination? ParseShortcutString(string shortcut)
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
}

