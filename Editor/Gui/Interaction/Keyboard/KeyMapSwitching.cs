#nullable enable
using System.IO;
using T3.Core.UserData;
using T3.Editor.Gui.UiHelpers;
using T3.Serialization;
using T3.SystemUi;

namespace T3.Editor.Gui.Interaction.Keyboard;

/// <summary>
/// Manages loading and switching of keyboard layouts
/// </summary>
internal static class KeyMapSwitching
{
    private static readonly KeyMap _factoryKeymap = CreateFactoryKeymap();
    public static KeyMap CurrentKeymap = _factoryKeymap;
    
    /// <summary>
    /// Requires user settings to be loaded already.
    /// </summary>
    internal static void Initialize()
    {
        LoadAllKeyMaps();
        CurrentKeymap = GetUserOrFactoryKeyMap();
    }

    public static bool TrySetKeyMap(string name)
    {
        var selectedKeyMap = KeyMaps.FirstOrDefault(t => t.Name == name);
        if (selectedKeyMap == null)
        {
            CurrentKeymap = _factoryKeymap;
            return false;
        }

        CurrentKeymap = selectedKeyMap;
        return true;
    }

    internal static void SaveKeyMap(KeyMap keyMap)
    {
        Directory.CreateDirectory(KeyMapFolder);
    
        keyMap.Name = keyMap.Name.Trim();
        if (string.IsNullOrEmpty(keyMap.Name))
        {
            keyMap.Name = "untitled";
        }

        var filepath = GetKeyMapFilePath(keyMap);
        JsonUtils.TrySaveJson(keyMap, filepath);
    }

    private static string GetKeyMapFilePath(KeyMap keyMap)
    {
        return Path.Combine(KeyMapFolder, keyMap.Name + ".json");
    }
    

    private static KeyMap GetUserOrFactoryKeyMap()
    {
        var selectedKeyBindingName = UserSettings.Config.KeyBindingName;
        if (string.IsNullOrWhiteSpace(selectedKeyBindingName))
        {
            return _factoryKeymap;
        }

        var userKeyBinding = KeyMaps.FirstOrDefault(t => t.Name == selectedKeyBindingName);
        if (userKeyBinding == null)
        {
            Log.Warning($"Couldn't load {selectedKeyBindingName}");
            return _factoryKeymap;
        }

        return userKeyBinding;
    }

    private static void LoadAllKeyMaps()
    {
        Directory.CreateDirectory(KeyMapFolder);
        Directory.CreateDirectory(DefaultKeyMapFolder);

        // copy default KeyBindings if not present
        foreach (var keyMap in Directory.EnumerateFiles(DefaultKeyMapFolder))
        {
            var targetPath = Path.Combine(KeyMapFolder, Path.GetFileName(keyMap));
            if (!File.Exists(targetPath))
                File.Copy(keyMap, targetPath);
        }

        foreach (var filepath in Directory.EnumerateFiles(KeyMapFolder))
        {
            try
            {
                var t = JsonUtils.TryLoadingJson<KeyMap>(filepath);
                if (t == null)
                {
                    Log.Debug($"Failed to load Keymap {filepath}");
                    continue;
                }

                KeyMaps.Add(t);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to load {filepath} : {e.Message}");
            }
        }
    }
    
    internal static readonly List<KeyMap> KeyMaps = [_factoryKeymap];
    private static string KeyMapFolder => Path.Combine(FileLocations.SettingsPath, FileLocations.KeyBindingSubFolder);
    private static string DefaultKeyMapFolder => Path.Combine(FileLocations.ReadOnlySettingsPath, FileLocations.KeyBindingSubFolder);

    
    private static KeyMap CreateFactoryKeymap()
    {
        var map= new KeyMap
                   {
                       Bindings =
                           [
                               new KeyBinding(UserActions.Save, new KeyCombination(Key.S, ctrl: true)),
                               new KeyBinding(UserActions.FocusSelection, new KeyCombination(Key.F)),
                               new KeyBinding(UserActions.Duplicate, new KeyCombination(Key.D, ctrl: true)),
                               new KeyBinding(UserActions.DeleteSelection, new KeyCombination(Key.Delete)),
                               new KeyBinding(UserActions.DeleteSelection, new KeyCombination(Key.Backspace)),
                               new KeyBinding(UserActions.CopyToClipboard, new KeyCombination(Key.C, ctrl: true)),
                               new KeyBinding(UserActions.PasteFromClipboard, new KeyCombination(Key.V, ctrl: true)),
                               new KeyBinding(UserActions.PasteValues, new KeyCombination(Key.V, ctrl: true, shift: true)),
                               new KeyBinding(UserActions.Undo, new KeyCombination(Key.Z, ctrl: true)),
                               new KeyBinding(UserActions.Redo, new KeyCombination(Key.Z, ctrl: true, shift: true)),

                               // Playback controls
                               new KeyBinding(UserActions.PlaybackForward, new KeyCombination(Key.L)),
                               new KeyBinding(UserActions.PlaybackForwardHalfSpeed, new KeyCombination(Key.L, shift: true)),
                               new KeyBinding(UserActions.PlaybackBackwards, new KeyCombination(Key.J)),
                               new KeyBinding(UserActions.PlaybackStop, new KeyCombination(Key.K)),
                               new KeyBinding(UserActions.PlaybackToggle, new KeyCombination(Key.Space)),
                               new KeyBinding(UserActions.PlaybackPreviousFrame, new KeyCombination(Key.CursorLeft, shift: true)),
                               new KeyBinding(UserActions.PlaybackNextFrame, new KeyCombination(Key.CursorRight, shift: true)),
                               new KeyBinding(UserActions.PlaybackJumpToStartTime, new KeyCombination(Key.Home)),
                               new KeyBinding(UserActions.PlaybackJumpToNextKeyframe, new KeyCombination(Key.Period)),
                               new KeyBinding(UserActions.PlaybackJumpToPreviousKeyframe, new KeyCombination(Key.Comma)),

                               // Timeline actions
                               new KeyBinding(UserActions.InsertKeyframe, new KeyCombination(Key.C)),
                               new KeyBinding(UserActions.InsertKeyframeWithIncrement, new KeyCombination(Key.C, shift: true)),
                               new KeyBinding(UserActions.ToggleAnimationPinning, new KeyCombination(Key.K, shift: true)),
                               new KeyBinding(UserActions.SetStartTime, new KeyCombination(Key.B)),
                               new KeyBinding(UserActions.SetEndTime, new KeyCombination(Key.N)),
                               new KeyBinding(UserActions.TapBeatSync, new KeyCombination(Key.Z)),
                               new KeyBinding(UserActions.TapBeatSyncMeasure, new KeyCombination(Key.X)),

                               // Graph window actions
                               new KeyBinding(UserActions.ToggleDisabled, new KeyCombination(Key.D, shift: true)),
                               new KeyBinding(UserActions.ToggleBypassed, new KeyCombination(Key.B, shift: true)),
                               new KeyBinding(UserActions.PinToOutputWindow, new KeyCombination(Key.P)),
                               new KeyBinding(UserActions.DisplayImageAsBackground, new KeyCombination(Key.P, ctrl: true)),
                               new KeyBinding(UserActions.ClearBackgroundImage, new KeyCombination(Key.P, ctrl: true, shift: true)),
                               new KeyBinding(UserActions.LayoutSelection, new KeyCombination(Key.G)),
                               new KeyBinding(UserActions.AddAnnotation, new KeyCombination(Key.A, shift: true)),
                               new KeyBinding(UserActions.AddComment, new KeyCombination(Key.C, shift: true, ctrl: true)),
                               new KeyBinding(UserActions.ToggleVariationsWindow, new KeyCombination(Key.V, alt: true)),
                               new KeyBinding(UserActions.SearchGraph, new KeyCombination(Key.F, ctrl: true)),
                               new KeyBinding(UserActions.OpenOperator, new KeyCombination(Key.I)),
                               new KeyBinding(UserActions.CloseOperator, new KeyCombination(Key.U)),
                               new KeyBinding(UserActions.RenameChild, new KeyCombination(Key.Return)),

                               // Navigation
                               new KeyBinding(UserActions.NavigateBackwards, new KeyCombination(Key.CursorLeft, alt: true)),
                               new KeyBinding(UserActions.NavigateForward, new KeyCombination(Key.CursorRight, alt: true)),
                               new KeyBinding(UserActions.SelectToAbove, new KeyCombination(Key.CursorUp)),
                               new KeyBinding(UserActions.SelectToRight, new KeyCombination(Key.CursorRight)),
                               new KeyBinding(UserActions.SelectToBelow, new KeyCombination(Key.CursorDown)),
                               new KeyBinding(UserActions.SelectToLeft, new KeyCombination(Key.CursorLeft)),

                               // Camera controls
                               new KeyBinding(UserActions.CameraLeft, new KeyCombination(Key.A)),
                               new KeyBinding(UserActions.CameraRight, new KeyCombination(Key.D)),
                               new KeyBinding(UserActions.CameraForward, new KeyCombination(Key.W)),
                               new KeyBinding(UserActions.CameraBackward, new KeyCombination(Key.S)),
                               new KeyBinding(UserActions.CameraUp, new KeyCombination(Key.E)),
                               new KeyBinding(UserActions.CameraDown, new KeyCombination(Key.Q)),
                               // Camera reset and focus
                               new KeyBinding(UserActions.CameraReset, new KeyCombination(Key.F)),
                               new KeyBinding(UserActions.CameraFocusSelection, new KeyCombination(Key.C)),

                               // Layout and window management
                               new KeyBinding(UserActions.ToggleAllUiElements, new KeyCombination(Key.Esc, shift: true)),
                               new KeyBinding(UserActions.ToggleFullscreen, new KeyCombination(Key.F11)),
                               new KeyBinding(UserActions.ToggleFocusMode, new KeyCombination(Key.F12))

                               // TODO: these methods are overly complicated and longer than the original code.
                               // private static IEnumerable<KeyActionHandling> GenerateNumberedBindings(UserActions loadBase, UserActions saveBaseuseCtrl)
                               // {
                               //     var keys = new[] { Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9 };
                               //
                               //     for (int i = 0; i < 10; i++)
                               //     {
                               //         var loadAction = loadBase + i;
                               //         var saveAction = saveBase + i;
                               //
                               //         if (useCtrl)
                               //         {
                               //             yield return new(loadAction, new(keys[i], ctrl: true));
                               //             yield return new(saveAction, new(keys[i], ctrl: true, shift: true));
                               //         }
                               //         else
                               //         {
                               //             yield return new(loadAction, new(keys[i]));
                               //             yield return new(saveAction, new(keys[i], shift: true));
                               //         }
                               //     }
                               // }

                               // private static IEnumerable<KeyActionHandling> GenerateLayoutBindings()
                               // {
                               //     var fKeys = new[] { Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10 };
                               //
                               //     for (int i = 0; i < 10; i++)
                               //     {
                               //         yield return new(UserActions.LoadLayout0 + i, new(fKeys[i]));
                               //         yield return new(UserActions.SaveLayout0 + i, new(fKeys[i], ctrl: true));
                               //     }
                               // }                           
                           ],
                       Name = "TiXL Default (QUERTY)",
                       Author = "TiXL Community",
                       ReadOnly = true,
                   };
        
        map.UpdateShortcutLabels();
        return map;
    }

    public static void CloneCurrentKeymap()
    {
        var newKeymap = CurrentKeymap.Clone();

        newKeymap.Name += "Custom";
        newKeymap.Author = UserSettings.Config.UserName;
        KeyMaps.Add(newKeymap);
        CurrentKeymap = newKeymap;
    }
}