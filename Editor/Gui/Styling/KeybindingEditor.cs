#nullable enable
using ImGuiNET;
using T3.Core.UserData;
using T3.Editor.Gui.Interaction;
using T3.Editor.Gui.UiHelpers;
using T3.Editor.UiModel.InputsAndTypes;

namespace T3.Editor.Gui.Styling;

/// <summary>
/// User interface switching and adjusting KeyBindings...
/// </summary>


internal static class KeyBindingEditor
{
    public static void DrawEditor()
    {
        KeyBindingHandling.Initialize();
        if (!_initialized)
        {
            _currentKeyBinding = KeyBindingHandling.GetUserOrFactoryKeyBinding();
           // _currentKeyBindingWithoutChanges = _currentKeyBinding.Clone();
            _initialized = true;

        }
        var colorFields = typeof(UiColors).GetFields();
        var colorVariationFields = typeof(ColorVariations).GetFields();

        var xxx = typeof(ColorVariations);
        var ff = xxx.UnderlyingSystemType;

        var xxx2 = ff.GetFields();

        
            FormInputs.SetIndent(100);
            
            if (FormInputs.AddDropdown(ref UserSettings.Config.KeyBindingName,
                                   KeyBindingHandling.KeyBindings.Select(t => t.Name),
                                   "KeyBinding",
                                   $"""
                                   Edit the shortcuts as you wish you'll find the  {FileLocations.SettingsPath}.
                                   """))
            {
                var selectedKeyBinding = KeyBindingHandling.KeyBindings.FirstOrDefault(t => t.Name == UserSettings.Config.KeyBindingName);
                if (selectedKeyBinding != null)
                {
                    KeyBindingHandling.SetKeyBindingAsUserKeyBinding(selectedKeyBinding);
                    _currentKeyBinding = selectedKeyBinding;
                    // _currentKeyBindingWithoutChanges = _currentKeyBinding.Clone();
                }
            }

            string currentName = KeyboardBinding.CurrentBindingSetName;
            string currentAuthor = KeyboardBinding.CurrentBindingSetAuthor;
            
            FormInputs.AddVerticalSpace();
            FormInputs.AddStringInput("Name", ref _currentKeyBinding.Name!);
            FormInputs.AddStringInput("Author", ref _currentKeyBinding.Author!);
            _somethingChanged |= _currentKeyBinding.Name != _currentKeyBindingWithoutChanges.Name;
            _somethingChanged |= _currentKeyBinding.Author != _currentKeyBindingWithoutChanges.Author;
            ImGui.TextUnformatted($"Current KeyBinding: {currentName} by {currentAuthor}");
   
        
        FormInputs.AddVerticalSpace();
        FormInputs.ApplyIndent();
        if (CustomComponents.DisablableButton("Save", _somethingChanged))
        {
           /* KeyBindingHandling.SaveKeyBinding(_currentKeyBinding);
            UserSettings.Config.KeyBindingName = _currentKeyBinding.Name;
            var currentFromName = KeyBindingHandling.KeyBindings.FirstOrDefault(t => t.Name == UserSettings.Config.KeyBindingName);
            if (currentFromName == null)
            {
                Log.Error("Saving KeyBinding failed");
                return;
            }
            
            _currentKeyBinding = currentFromName;
            _currentKeyBindingWithoutChanges = currentFromName.Clone();*/
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Delete"))
        {
           /* KeyBindingHandling.DeleteKeyBinding(_currentKeyBinding);
            _currentKeyBinding = KeyBindingHandling.FactoryKeyBinding.Clone();
            _currentKeyBindingWithoutChanges = KeyBindingHandling.FactoryKeyBinding.Clone();*/
        }
        
        FormInputs.AddVerticalSpace();
        ImGui.Separator();
        FormInputs.AddVerticalSpace();
        
        _somethingChanged = false;
        DrawKeybindingEdits();
        
    }



    private static void DrawKeybindingEdits()
    {
        

            if (ImGui.BeginTable("Shortcuts", 2,
                                         ImGuiTableFlags.BordersInnerH))
        {
            foreach (var value in Enum.GetValues<UserActions>())
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                var actionName = CustomComponents.HumanReadablePascalCase(Enum.GetName(value));
                var shortcuts = KeyboardBinding.ListKeyboardShortcuts(value, false);
               // var triggerkind 
                var hasShortcut = !string.IsNullOrEmpty(shortcuts);
                ImGui.PushStyleColor(ImGuiCol.Text, hasShortcut ? UiColors.Text : UiColors.TextMuted.Rgba);
                ImGui.TextUnformatted(actionName);

                ImGui.TableSetColumnIndex(1);

                if (hasShortcut)
                {
                    ImGui.PushFont(Fonts.FontBold);
                    ImGui.TextUnformatted(shortcuts);
                    ImGui.PopFont();

                }
                
            }
            
            ImGui.PopStyleColor();
            ImGui.EndTable();
        }
    }

    private static bool _somethingChanged;
    private static bool _initialized;
    private static KeyBindingHandling.KeyBinding _currentKeyBinding= new();
    private static KeyBindingHandling.KeyBinding _currentKeyBindingWithoutChanges = new();
    public static readonly object Dummy = new();
}
