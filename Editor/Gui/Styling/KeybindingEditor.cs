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
            //ImGui.TextUnformatted($"Current KeyBinding: {currentName} by {currentAuthor}");
   
        
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
        // Calculate available width for centering
        var availableWidth = ImGui.GetContentRegionAvail().X;

        // Draw editing controls (centered) if an action is selected
        if (_selectedAction.HasValue)
        {
            ImGui.BeginChild("EditingControls", new System.Numerics.Vector2(availableWidth, ImGui.GetTextLineHeight() * 5), true);

            ImGui.TextUnformatted($"Editing: {CustomComponents.HumanReadablePascalCase(Enum.GetName(_selectedAction.Value))}");

            // Input field for the shortcut
            
            ImGui.SetNextItemWidth(242);
            ImGui.InputText("##ShortcutInput", ref _editingShortcut, 256);

            // Save and cancel buttons
            
            if (ImGui.Button("Save", new System.Numerics.Vector2(80, 0)))
            {
              //  KeyboardBinding.SetKeyboardShortcut(_selectedAction.Value, _editingShortcut);
              //  _selectedAction = null;
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel", new System.Numerics.Vector2(80, 0)))
            {
             //   _selectedAction = null;
            }

            ImGui.SameLine();
            if (ImGui.Button("Clear", new System.Numerics.Vector2(80, 0)))
            {
              //  KeyboardBinding.ClearKeyboardShortcut(_selectedAction.Value);
               // _selectedAction = null;
            }

            ImGui.EndChild();
            ImGui.Spacing();
        }

        // Draw the table of shortcuts
        if (ImGui.BeginTable("Shortcuts", 2, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY))
        {
            // Make the table fill remaining vertical space
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch, 0.5f);
            ImGui.TableSetupColumn("Shortcut", ImGuiTableColumnFlags.WidthStretch, 0.5f);

            foreach (var value in Enum.GetValues<UserActions>())
            {
                ImGui.TableNextRow();

                // Set row background color if selected
                if (_selectedAction == value)
                {
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0,
                        ImGui.GetColorU32(ImGuiCol.Header));
                }

                ImGui.TableSetColumnIndex(0);
                var actionName = CustomComponents.HumanReadablePascalCase(Enum.GetName(value));
                var shortcuts = KeyboardBinding.ListKeyboardShortcuts(value, false);

                // Make the first column selectable
                if (ImGui.Selectable(actionName, _selectedAction == value,
                    ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap))
                {
                    _selectedAction = value;
                    _editingShortcut = shortcuts ?? "";
                }

                ImGui.TableSetColumnIndex(1);

                if (!string.IsNullOrEmpty(shortcuts))
                {
                    ImGui.PushFont(Fonts.FontBold);
                    ImGui.TextUnformatted(shortcuts);
                    ImGui.PopFont();
                }
            }

            ImGui.EndTable();
        }
    }

    private static bool _somethingChanged;
    private static bool _initialized;
    private static KeyBindingHandling.KeyBinding _currentKeyBinding= new();
    private static KeyBindingHandling.KeyBinding _currentKeyBindingWithoutChanges = new();
    public static readonly object Dummy = new();

    private static UserActions? _selectedAction = null;
    private static string _editingShortcut = "";
}
