#nullable enable
using ImGuiNET;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using T3.Core.UserData;
using T3.Editor.Gui.UiHelpers;
using T3.SystemUi;

namespace T3.Editor.Gui.Interaction;

/// <summary>
/// UserAction represents single atomic commands that can be mapped to keyboard shortcuts
/// </summary>
internal enum UserActions
{
    // General
    Undo,
    Redo,
    Save,
    FocusSelection,
    DeleteSelection,
    CopyToClipboard,
    PasteFromClipboard,
    PasteValues,
    New,

    // Playback
    PlaybackForward,
    PlaybackForwardHalfSpeed,
    PlaybackBackwards,
    PlaybackNextFrame,
    PlaybackPreviousFrame,
    PlaybackStop,
    PlaybackToggle,
    PlaybackJumpBack,
    PlaybackJumpToNextKeyframe,
    PlaybackJumpToPreviousKeyframe,
    PlaybackJumpToEndTime,
    PlaybackJumpToStartTime,

    // Timeline
    SetStartTime,
    SetEndTime,
    InsertKeyframe,
    InsertKeyframeWithIncrement,
    TapBeatSync,
    TapBeatSyncMeasure,
    ToggleAnimationPinning,

    // Graph
    PinToOutputWindow,
    DisplayImageAsBackground,
    ClearBackgroundImage,
    Duplicate,
    LayoutSelection,
    ToggleDisabled,
    ToggleBypassed,
    AddAnnotation,
    AddComment,
    RenameChild,
    ToggleSnapshotControl,

    // Navigation
    ScrollLeft,
    ScrollRight,
    ScrollUp,
    ScrollDown,
    ZoomIn,
    ZoomOut,
    NavigateBackwards,
    NavigateForward,
    SelectToAbove,
    SelectToRight,
    SelectToBelow,
    SelectToLeft,
    SearchGraph,
    OpenOperator,
    CloseOperator,

    //camera controls
    CameraLeft,
    CameraRight,
    CameraForward,
    CameraBackward,
    CameraUp,
    CameraDown,
    CameraReset,
    CameraFocusSelection,

    // Layout and window management
    ToggleAllUiElements,
    ToggleVariationsWindow,
    ToggleFullscreen,
    ToggleFocusMode,

    // Layouts (0-9)
    LoadLayout0, LoadLayout1, LoadLayout2, LoadLayout3, LoadLayout4,
    LoadLayout5, LoadLayout6, LoadLayout7, LoadLayout8, LoadLayout9,
    SaveLayout0, SaveLayout1, SaveLayout2, SaveLayout3, SaveLayout4,
    SaveLayout5, SaveLayout6, SaveLayout7, SaveLayout8, SaveLayout9,

    // Bookmarks (0-9)
    LoadBookmark0, LoadBookmark1, LoadBookmark2, LoadBookmark3, LoadBookmark4,
    LoadBookmark5, LoadBookmark6, LoadBookmark7, LoadBookmark8, LoadBookmark9,
    SaveBookmark0, SaveBookmark1, SaveBookmark2, SaveBookmark3, SaveBookmark4,
    SaveBookmark5, SaveBookmark6, SaveBookmark7, SaveBookmark8, SaveBookmark9,
}

internal static class UserActionRegistry
{
    private static readonly HashSet<UserActions> _deferredActions = new();

    public static bool WasActionQueued(UserActions action)
    {
        return _deferredActions.Remove(action);
    }

    public static void QueueAction(UserActions action)
    {
        _deferredActions.Add(action);
    }
}

internal sealed class KeyboardBinding
{
    private static readonly List<KeyboardBinding> _bindings = new();
    private static readonly Dictionary<UserActions, string> _shortcutLabels = new();
    private static bool _initialized;
    private static bool _anyKeysPressed;

    // Metadata for the current binding set
    public static string CurrentBindingSetName { get; private set; } = "Default";
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

    internal static void InitFrame()
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
        var jsonPath = GetBindingsFilePath();

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

    private static string GetBindingsFilePath()
    {
        var folder = Path.Combine(FileLocations.SettingsPath, FileLocations.KeyBindingSubFolder);
        return Path.Combine(folder, "KeyboardBindings.json");
    }

    private static void ParseJsonBindings(KeyboardBindingsJson? jsonBindings)
    {
        if (jsonBindings?.KeyboardBindings == null)
        {
            InitializeDefaultBindings();
            return;
        }

        // Load metadata
        CurrentBindingSetName = jsonBindings.Name ?? "Default";
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
    public static bool LoadCustomBindings(string fileName)
    {
        try
        {
            var folder = Path.Combine(FileLocations.SettingsPath, FileLocations.KeyBindingSubFolder);
            var customPath = Path.Combine(folder, fileName.EndsWith(".json") ? fileName : fileName + ".json");

            if (!File.Exists(customPath))
                return false;

            var json = File.ReadAllText(customPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var jsonBindings = JsonSerializer.Deserialize<KeyboardBindingsJson>(json, options);
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

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Thread.Sleep(100); // Ensure directory is created before writing
        }

        File.WriteAllText(path, JsonSerializer.Serialize(jsonBindings, options));
    }

    private static void InitializeDefaultBindings()
    {
        _bindings.Clear();
        _bindings.AddRange(CreateDefaultBindings());
    }

    private static IEnumerable<KeyboardBinding> CreateDefaultBindings()
    {
        // General actions
        yield return new(UserActions.Save, new(Key.S, ctrl: true));
        yield return new(UserActions.FocusSelection, new(Key.F), needsWindowHover: true);
        yield return new(UserActions.Duplicate, new(Key.D, ctrl: true), needsWindowFocus: true);
        yield return new(UserActions.DeleteSelection, new(Key.Delete), needsWindowFocus: true);
        yield return new(UserActions.DeleteSelection, new(Key.Backspace), needsWindowFocus: true);
        yield return new(UserActions.CopyToClipboard, new(Key.C, ctrl: true), needsWindowFocus: true);
        yield return new(UserActions.PasteFromClipboard, new(Key.V, ctrl: true), needsWindowFocus: true);
        yield return new(UserActions.PasteValues, new(Key.V, ctrl: true, shift: true), needsWindowFocus: true);
        yield return new(UserActions.Undo, new(Key.Z, ctrl: true));
        yield return new(UserActions.Redo, new(Key.Z, ctrl: true, shift: true));

        // Playback controls
        yield return new(UserActions.PlaybackForward, new(Key.L));
        yield return new(UserActions.PlaybackForwardHalfSpeed, new(Key.L, shift: true));
        yield return new(UserActions.PlaybackBackwards, new(Key.J));
        yield return new(UserActions.PlaybackStop, new(Key.K));
        yield return new(UserActions.PlaybackToggle, new(Key.Space), keyPressOnly: true);
        yield return new(UserActions.PlaybackPreviousFrame, new(Key.CursorLeft, shift: true));
        yield return new(UserActions.PlaybackNextFrame, new(Key.CursorRight, shift: true));
        yield return new(UserActions.PlaybackJumpToStartTime, new(Key.Home));
        yield return new(UserActions.PlaybackJumpToNextKeyframe, new(Key.Period));
        yield return new(UserActions.PlaybackJumpToPreviousKeyframe, new(Key.Comma));

        // Timeline actions
        yield return new(UserActions.InsertKeyframe, new(Key.C), needsWindowFocus: true);
        yield return new(UserActions.InsertKeyframeWithIncrement, new(Key.C, shift: true));
        yield return new(UserActions.ToggleAnimationPinning, new(Key.K, shift: true));
        yield return new(UserActions.SetStartTime, new(Key.B));
        yield return new(UserActions.SetEndTime, new(Key.N));
        yield return new(UserActions.TapBeatSync, new(Key.Z), keyPressOnly:true);
        yield return new(UserActions.TapBeatSyncMeasure, new(Key.X), keyPressOnly: true);

        // Graph window actions
        yield return new(UserActions.ToggleDisabled, new(Key.D, shift: true), needsWindowFocus: true);
        yield return new(UserActions.ToggleBypassed, new(Key.B, shift: true), needsWindowFocus: true);
        yield return new(UserActions.PinToOutputWindow, new(Key.P));//, needsWindowFocus: true);
        yield return new(UserActions.DisplayImageAsBackground, new(Key.P, ctrl: true));
        yield return new(UserActions.ClearBackgroundImage, new(Key.P, ctrl: true, shift: true), needsWindowFocus: true);
        yield return new(UserActions.LayoutSelection, new(Key.G));
        yield return new(UserActions.AddAnnotation, new(Key.A, shift: true), needsWindowFocus: true);
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

        // Camera controls

       // yield return new(UserActions.ScrollLeft, new(Key.A), needsWindowHover: true);

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
        public string Name { get; set; } = "Default";
        public string Author { get; set; } = "Community";
        public List<KeyboardBindingJson> KeyboardBindings { get; set; } = new();
    }
    #endregion
}

internal sealed class KeyCombination
{
    public Key Key { get; }
    public bool Ctrl { get; }
    public bool Alt { get; }
    public bool Shift { get; }

    public KeyCombination(Key key, bool ctrl = false, bool alt = false, bool shift = false)
    {
        Key = key;
        Ctrl = ctrl;
        Alt = alt;
        Shift = shift;
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