#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.IO;
using T3.Core.DataTypes.Vector;
using T3.Core.UserData;
using T3.Editor.Gui.UiHelpers;
using T3.Serialization;

namespace T3.Editor.Gui.Styling;

public static class KeyBindingHandling
{
    /// <summary>
    /// Requires user settings to be loaded already.
    /// </summary>
    internal static void Initialize()
    {
        InitializeFactoryDefault();
        LoadKeyBindings();
        ApplyUserConfigKeyBinding();
    }

    internal static void SetKeyBindingAsUserKeyBinding(KeyBinding KeyBinding)
    {
        UserSettings.Config.KeyBindingName = KeyBinding.Name;
        UserSettings.Save();
        ApplyKeyBinding(KeyBinding);

        T3Style.Apply();
    }

    internal static void SaveKeyBinding(KeyBinding KeyBinding)
    {
        Directory.CreateDirectory(KeyBindingFolder);

        KeyBinding.Name = KeyBinding.Name.Trim();
        if (string.IsNullOrEmpty(KeyBinding.Name))
        {
            KeyBinding.Name = "untitled";
        }

        var combine = GetKeyBindingFilepath(KeyBinding);
        var filepath = combine;

        StoreAllColors(KeyBinding);

        JsonUtils.TrySaveJson(KeyBinding, filepath);
        LoadKeyBindings();
    }

    internal static void DeleteKeyBinding(KeyBinding KeyBinding)
    {
        var filepath = GetKeyBindingFilepath(KeyBinding);
        if (!File.Exists(filepath))
        {
            Log.Warning($"{filepath} does not exist?");
            return;
        }

        File.Delete(filepath);
        ApplyKeyBinding(FactoryKeyBinding);
        LoadKeyBindings();
        UserSettings.Config.KeyBindingName = string.Empty;
    }

    internal static KeyBinding GetUserOrFactoryKeyBinding()
    {
        var selectedKeyBindingName = UserSettings.Config.KeyBindingName;
        if (string.IsNullOrWhiteSpace(selectedKeyBindingName))
        {
            return FactoryKeyBinding;
        }

        var userKeyBinding = KeyBindings.FirstOrDefault(t => t.Name == selectedKeyBindingName);
        if (userKeyBinding == null)
        {
            Log.Warning($"Couldn't load {selectedKeyBindingName}");
            return FactoryKeyBinding;
        }

        return userKeyBinding;
    }
    
    private static void LoadKeyBindings()
    {
        KeyBindings.Clear();
        
        Directory.CreateDirectory(KeyBindingFolder);
        Directory.CreateDirectory(DefaultKeyBindingFolder);

        // copy default KeyBindings if not present
        foreach (var KeyBinding in Directory.EnumerateFiles(DefaultKeyBindingFolder))
        {
            var targetPath = Path.Combine(KeyBindingFolder, Path.GetFileName(KeyBinding));
            if(!File.Exists(targetPath))
                File.Copy(KeyBinding, targetPath);
        }

        foreach (var filepath in Directory.EnumerateFiles(KeyBindingFolder))
        {
            try
            {
                var t = JsonUtils.TryLoadingJson<KeyBinding>(filepath);
                if (t == null)
                {
                    Log.Debug($"Failed to load KeyBinding {filepath}");
                    continue;
                }

                KeyBindings.Add(t);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to load {filepath} : {e.Message}");
            }
        }
    }

    private static void ApplyUserConfigKeyBinding()
    {
        var userKeyBinding = GetUserOrFactoryKeyBinding();

        ApplyKeyBinding(userKeyBinding);
    }
    


    /// <summary>
    /// Applies the colors to T3StyleColors
    /// </summary>
    /// <param name="KeyBinding"></param>
    private static void ApplyKeyBinding(KeyBinding KeyBinding)
    {
        var colorFields = typeof(UiColors).GetFields();
        foreach (var colorField in colorFields)
        {
            if (colorField.GetValue(KeyBindingEditor.Dummy) is not Color)
                continue;

            if (!KeyBinding.Colors.TryGetValue(colorField.Name, out var colorValue))
                continue;

            colorField.SetValue(KeyBindingEditor.Dummy, new Color(colorValue));
        }

        var variationFields = typeof(ColorVariations).GetFields();
        foreach (var varField in variationFields)
        {
            if (varField.GetValue(KeyBindingEditor.Dummy) is not ColorVariation)
                continue;

            if (!KeyBinding.Variations.TryGetValue(varField.Name, out var variation))
                continue;

            varField.SetValue(KeyBindingEditor.Dummy, variation.Clone());
        }

        FrameStats.Current.UiColorsChanged = true;
        T3Style.Apply();
    }

    private static string GetKeyBindingFilepath(KeyBinding KeyBinding)
    {
        return Path.Combine(KeyBindingFolder, KeyBinding.Name + ".json");
    }

    private static void StoreAllColors(KeyBinding KeyBinding)
    {
        var colorFields = typeof(UiColors).GetFields();
        foreach (var colorField in colorFields)
        {
            if (colorField.GetValue(KeyBindingEditor.Dummy) is not Color color)
                continue;

            KeyBinding.Colors[colorField.Name] = color;
        }

        var variationFields = typeof(ColorVariations).GetFields();
        foreach (var varField in variationFields)
        {
            if (varField.GetValue(KeyBindingEditor.Dummy) is not ColorVariation variation)
                continue;

            KeyBinding.Variations[varField.Name] = variation;
        }
    }


    private static void InitializeFactoryDefault()
    {
        FactoryKeyBinding = new KeyBindingHandling.KeyBinding();

        var colorFields = typeof(UiColors).GetFields();
        foreach (var f in colorFields)
        {
            if (f.GetValue(KeyBindingEditor.Dummy) is not Color color)
                continue;

            FactoryKeyBinding.Colors[f.Name] = color;
        }

        var variationFields = typeof(ColorVariations).GetFields();
        foreach (var v in variationFields)
        {
            if (v.GetValue(KeyBindingEditor.Dummy) is not ColorVariation variation)
                continue;

            FactoryKeyBinding.Variations[v.Name] = variation;
        }
    }

    internal static readonly List<KeyBinding> KeyBindings = [];
    internal static string KeyBindingFolder => Path.Combine(FileLocations.SettingsPath, FileLocations.KeyBindingSubFolder);
    private static string DefaultKeyBindingFolder => Path.Combine(FileLocations.ReadOnlySettingsPath, FileLocations.KeyBindingSubFolder);
    internal static KeyBinding FactoryKeyBinding = null!;
    
    
    // This public for serialization
    [SuppressMessage("ReSharper", "MemberCanBeInternal")]
    public sealed class KeyBinding
    {
        public string Name = "untitled";
        public string Author = "unknown";
        public Dictionary<string, Vector4> Colors = new();
        public Dictionary<string, ColorVariation> Variations = new();

        public KeyBinding Clone()
        {
            return new KeyBinding()
                       {
                           Name = Name,
                           Author = Author,
                           Colors = Colors.ToDictionary(entry => entry.Key,
                                                        entry => entry.Value),
                           Variations = Variations.ToDictionary(entry => entry.Key,
                                                                entry => entry.Value),
                       };
        }
    }
}