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
public static class KeyMapSwitching
{
    /// <summary>
    /// Requires user settings to be loaded already.
    /// </summary>
    internal static void Initialize()
    {
        //InitializeFactoryDefault();
        LoadAllKeyMaps();
        CurrentKeymap = GetUserOrFactoryKeyMap();
        //ApplyUserConfigKeyBinding();
    }

    public static bool TrySetKeyMap(string name)
    {
        var selectedKeyMap = KeyMaps.FirstOrDefault(t => t.Name == UserSettings.Config.KeyBindingName);
        if (selectedKeyMap == null)
        {
            CurrentKeymap = _factoryKeymap;
            return false;
            //_currentKeyBinding = selectedKeyBinding;
            //KeyMapSwitching.ApplyMapAndSaveUserSetting(selectedKeyBinding);
        }

        CurrentKeymap = selectedKeyMap;
        return true;
    }

    internal static void ApplyMapAndSaveUserSetting(KeyMap keyMap)
    {
        UserSettings.Config.KeyBindingName = keyMap.Name;
        UserSettings.Save();

        CurrentKeymap = keyMap;
        //ApplyKeyBinding(KeyBinding);
        //KeyActionHandling.LoadCustomBindings(KeyBinding.Name +".json");
    }

    
    // internal static void SaveKeyMap(KeyBinding keyMap)
    // {
    //     Directory.CreateDirectory(KeyMapFolder);
    //
    //     keyMap.Name = keyMap.Name.Trim();
    //     if (string.IsNullOrEmpty(keyMap.Name))
    //     {
    //         keyMap.Name = "untitled";
    //     }
    //
    //     var combine = GetKeyBindingFilepath(keyMap);
    //     var filepath = combine;
    //
    //     JsonUtils.TrySaveJson(keyMap, filepath);
    //     LoadAllKeyBindings();
    // }

    // internal static void DeleteKeyBinding(KeyBinding KeyBinding)
    // {
    //     var filepath = GetKeyBindingFilepath(KeyBinding);
    //     if (!File.Exists(filepath))
    //     {
    //         Log.Warning($"{filepath} does not exist?");
    //         return;
    //     }
    //
    //     File.Delete(filepath);
    //     ApplyKeyBinding(FactoryKeyBinding);
    //     LoadAllKeyBindings();
    //     UserSettings.Config.KeyBindingName = string.Empty;
    // }

    internal static KeyMap GetUserOrFactoryKeyMap()
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
        KeyMaps.Clear();
        
        Directory.CreateDirectory(KeyMapFolder);
        Directory.CreateDirectory(DefaultKeyMapFolder);

        // copy default KeyBindings if not present
        foreach (var keyMap in Directory.EnumerateFiles(DefaultKeyMapFolder))
        {
            var targetPath = Path.Combine(KeyMapFolder, Path.GetFileName(keyMap));
            if(!File.Exists(targetPath))
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

    // public static void ApplyUserConfigKeyBinding()
    // {
    //     var userKeyBinding = GetUserOrFactoryKeyMap();
    //
    //     ApplyKeyBinding(userKeyBinding);
    // }
    


    /// <summary>
    /// Activate the selected keymap
    /// </summary>
    // private static void ApplyKeyBinding(KeyMap keyMap)
    // {
    //     KeyActionHandling.LoadCustomBindings(keyMap.Name + ".json");
    // }

    // private static string GetKeyBindingFilepath(KeyBinding KeyBinding)
    // {
    //     return Path.Combine(KeyMapFolder, KeyBinding.Name + ".json");
    // }

    // private static void InitializeFactoryDefault()
    // {
    //     FactoryKeyBinding = new KeyBinding();  
    // }

    internal static readonly List<KeyMap> KeyMaps = [];
    
    internal static string KeyMapFolder => Path.Combine(FileLocations.SettingsPath, FileLocations.KeyBindingSubFolder);
    private static string DefaultKeyMapFolder => Path.Combine(FileLocations.ReadOnlySettingsPath, FileLocations.KeyBindingSubFolder);

    
    private static readonly KeyMap _factoryKeymap = CreateFactoryKeymap();
    
    public static KeyMap CurrentKeymap = _factoryKeymap;


    private static KeyMap CreateFactoryKeymap()
    {
        return new KeyMap
                   {
                       Bindings =
                           [
                               new KeyBinding(UserActions.Save, new KeyCombination(Key.S, ctrl: true), keyPressOnly: true),
                               new KeyBinding(UserActions.FocusSelection, new KeyCombination(Key.F), needsWindowHover: true),
                               new KeyBinding(UserActions.Duplicate, new KeyCombination(Key.D, ctrl: true), needsWindowFocus: true),
                               new KeyBinding(UserActions.DeleteSelection, new KeyCombination(Key.Delete), needsWindowFocus: true),
                               new KeyBinding(UserActions.DeleteSelection, new KeyCombination(Key.Backspace), needsWindowFocus: true),
                               new KeyBinding(UserActions.CopyToClipboard, new KeyCombination(Key.C, ctrl: true), needsWindowFocus: true),
                               new KeyBinding(UserActions.PasteFromClipboard, new KeyCombination(Key.V, ctrl: true), needsWindowFocus: true),
                               new KeyBinding(UserActions.PasteValues, new KeyCombination(Key.V, ctrl: true, shift: true), needsWindowFocus: true),
                               new KeyBinding(UserActions.Undo, new KeyCombination(Key.Z, ctrl: true), keyPressOnly: true),
                               new KeyBinding(UserActions.Redo, new KeyCombination(Key.Z, ctrl: true, shift: true), keyPressOnly: true),

                               // Playback controls
                               new KeyBinding(UserActions.PlaybackForward, new KeyCombination(Key.L), keyPressOnly: true),
                               new KeyBinding(UserActions.PlaybackForwardHalfSpeed, new KeyCombination(Key.L, shift: true), keyPressOnly: true),
                               new KeyBinding(UserActions.PlaybackBackwards, new KeyCombination(Key.J), keyPressOnly: true),
                               new KeyBinding(UserActions.PlaybackStop, new KeyCombination(Key.K), keyPressOnly: true),
                               new KeyBinding(UserActions.PlaybackToggle, new KeyCombination(Key.Space), keyPressOnly: true),
                               new KeyBinding(UserActions.PlaybackPreviousFrame, new KeyCombination(Key.CursorLeft, shift: true)),
                               new KeyBinding(UserActions.PlaybackNextFrame, new KeyCombination(Key.CursorRight, shift: true)),
                               new KeyBinding(UserActions.PlaybackJumpToStartTime, new KeyCombination(Key.Home), keyPressOnly: true),
                               new KeyBinding(UserActions.PlaybackJumpToNextKeyframe, new KeyCombination(Key.Period)),
                               new KeyBinding(UserActions.PlaybackJumpToPreviousKeyframe, new KeyCombination(Key.Comma)),

                               // Timeline actions
                               new KeyBinding(UserActions.InsertKeyframe, new KeyCombination(Key.C), needsWindowFocus: true, keyPressOnly: true),
                               new KeyBinding(UserActions.InsertKeyframeWithIncrement, new KeyCombination(Key.C, shift: true)),
                               new KeyBinding(UserActions.ToggleAnimationPinning, new KeyCombination(Key.K, shift: true)),
                               new KeyBinding(UserActions.SetStartTime, new KeyCombination(Key.B), keyPressOnly: true),
                               new KeyBinding(UserActions.SetEndTime, new KeyCombination(Key.N), keyPressOnly: true),
                               new KeyBinding(UserActions.TapBeatSync, new KeyCombination(Key.Z), keyPressOnly: true),
                               new KeyBinding(UserActions.TapBeatSyncMeasure, new KeyCombination(Key.X), keyPressOnly: true),

                               // Graph window actions
                               new KeyBinding(UserActions.ToggleDisabled, new KeyCombination(Key.D, shift: true), needsWindowFocus: true),
                               new KeyBinding(UserActions.ToggleBypassed, new KeyCombination(Key.B, shift: true), needsWindowFocus: true),
                               new KeyBinding(UserActions.PinToOutputWindow, new KeyCombination(Key.P), keyPressOnly: true), //, needsWindowFocus: true),
                               new KeyBinding(UserActions.DisplayImageAsBackground, new KeyCombination(Key.P, ctrl: true)),
                               new KeyBinding(UserActions.ClearBackgroundImage, new KeyCombination(Key.P, ctrl: true, shift: true), needsWindowFocus: true),
                               new KeyBinding(UserActions.LayoutSelection, new KeyCombination(Key.G)),
                               new KeyBinding(UserActions.AddAnnotation, new KeyCombination(Key.A, shift: true), needsWindowFocus: true, keyPressOnly: true),
                               new KeyBinding(UserActions.AddComment, new KeyCombination(Key.C, shift: true, ctrl: true), needsWindowFocus: true),
                               new KeyBinding(UserActions.ToggleVariationsWindow, new KeyCombination(Key.V, alt: true)),
                               new KeyBinding(UserActions.SearchGraph, new KeyCombination(Key.F, ctrl: true)),
                               new KeyBinding(UserActions.OpenOperator, new KeyCombination(Key.I), needsWindowFocus: true),
                               new KeyBinding(UserActions.CloseOperator, new KeyCombination(Key.U), needsWindowFocus: true),
                               new KeyBinding(UserActions.RenameChild, new KeyCombination(Key.Return), needsWindowFocus: true),

                               // Navigation
                               new KeyBinding(UserActions.NavigateBackwards, new KeyCombination(Key.CursorLeft, alt: true)),
                               new KeyBinding(UserActions.NavigateForward, new KeyCombination(Key.CursorRight, alt: true)),
                               new KeyBinding(UserActions.SelectToAbove, new KeyCombination(Key.CursorUp), needsWindowFocus: true),
                               new KeyBinding(UserActions.SelectToRight, new KeyCombination(Key.CursorRight), needsWindowFocus: true),
                               new KeyBinding(UserActions.SelectToBelow, new KeyCombination(Key.CursorDown), needsWindowFocus: true),
                               new KeyBinding(UserActions.SelectToLeft, new KeyCombination(Key.CursorLeft), needsWindowFocus: true),

                               // Camera controls
                               new KeyBinding(UserActions.CameraLeft, new KeyCombination(Key.A), needsWindowHover: true, keyHoldOnly: true),
                               new KeyBinding(UserActions.CameraRight, new KeyCombination(Key.D), needsWindowHover: true, keyHoldOnly: true),
                               new KeyBinding(UserActions.CameraForward, new KeyCombination(Key.W), needsWindowHover: true, keyHoldOnly: true),
                               new KeyBinding(UserActions.CameraBackward, new KeyCombination(Key.S), needsWindowHover: true, keyHoldOnly: true),
                               new KeyBinding(UserActions.CameraUp, new KeyCombination(Key.E), needsWindowHover: true, keyHoldOnly: true),
                               new KeyBinding(UserActions.CameraDown, new KeyCombination(Key.Q), needsWindowHover: true, keyHoldOnly: true),
                               // Camera reset and focus
                               new KeyBinding(UserActions.CameraReset, new KeyCombination(Key.F), needsWindowHover: true),
                               new KeyBinding(UserActions.CameraFocusSelection, new KeyCombination(Key.C), needsWindowHover: true),

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
                       Author = "TiXL Community"
                   };
    }    

}