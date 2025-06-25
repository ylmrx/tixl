#nullable enable
using ImGuiNET;
using T3.Core.UserData;
using T3.Editor.Gui.Styling;
using T3.Editor.Gui.UiHelpers;

namespace T3.Editor.Gui.Interaction.Keyboard;

/// <summary>
/// User interface switching and adjusting KeyBindings...
/// </summary>
internal static class KeyBindingEditor
{
    private static KeyMap CurrentKeyMap => KeyMapSwitching.CurrentKeymap;

    private static KeyMap _currentKeyBindingWithoutChanges = KeyMapSwitching.CurrentKeymap.Clone();

    public static void DrawEditor()
    {
        FormInputs.SetIndent(100);

        // Draw Keymap list
        var userSelection = UserSettings.Config.KeyBindingName;
        if (FormInputs.AddDropdown(ref userSelection,
                                   KeyMapSwitching.KeyMaps.Select(t => t.Name),
                                   "KeyBinding",
                                   $"Edit the shortcuts as you wish you'll find the {FileLocations.SettingsPath}."))
        {
            if (KeyMapSwitching.TrySetKeyMap(userSelection))
            {
                UserSettings.Config.KeyBindingName = userSelection;
                _currentKeyBindingWithoutChanges = CurrentKeyMap.Clone();
            }
            else
            {
                Log.Warning($"Failed to find keymap {userSelection}");
            }
        }

        FormInputs.AddVerticalSpace();
        FormInputs.AddStringInput("Name", ref KeyMapSwitching.CurrentKeymap.Name);
        FormInputs.AddStringInput("Author", ref KeyMapSwitching.CurrentKeymap.Author);

        _somethingChanged |= KeyMapSwitching.CurrentKeymap.Name != _currentKeyBindingWithoutChanges.Name;
        _somethingChanged |= KeyMapSwitching.CurrentKeymap.Author != _currentKeyBindingWithoutChanges.Author;

        FormInputs.AddVerticalSpace();
        FormInputs.ApplyIndent();

        // Add "Create New" button
        if (ImGui.Button("Create New"))
        {
            var newKeymap = KeyMapSwitching.CurrentKeymap.Clone();

            newKeymap.Name += "Custom";
            newKeymap.Author = UserSettings.Config.UserName;
            KeyMapSwitching.CurrentKeymap = newKeymap;
            _somethingChanged = true;
        }

        
        if (CustomComponents.DisablableButton("Save", _somethingChanged))
        {
            // TODO: and check if saving worked
            //KeyBindingHandling.SaveKeyBinding(_currentKeyBinding);
            
            UserSettings.Config.KeyBindingName = KeyMapSwitching.CurrentKeymap.Name;
            KeyMapSwitching.Initialize(); // Reloads all
            //CurrentKeymap = GetUserOrFactoryKeyMap()
            
            // var currentFromName = KeyBindingHandling.KeyBindings.FirstOrDefault(t => t.Name == UserSettings.Config.KeyBindingName);
            // if (currentFromName == null)
            // {
            //     Log.Error("Saving KeyBinding failed");
            //     return;
            // }
            //
            // _currentKeyBinding = currentFromName;
           // _currentKeyBindingWithoutChanges = currentFromName.Clone();
        }

        /*
        ImGui.SameLine();
        if (ImGui.Button("Delete"))
        {
            if (_currentKeyBinding != null && KeyMapSwitching.CurrentKeymap.Name != KeyBindingHandling.FactoryKeyBinding.Name)
            {
                KeyBindingHandling.DeleteKeyBinding(_currentKeyBinding);
                _currentKeyBinding = KeyBindingHandling.FactoryKeyBinding;
                UserSettings.Config.KeyBindingName = KeyBindingHandling.FactoryKeyBinding.Name;
                UserSettings.Save();
                _somethingChanged = false;
            }
        }*/
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

        // Always show editing controls area
        ImGui.BeginChild("EditingControls", new Vector2(availableWidth, ImGui.GetTextLineHeight() * 5), true);

        // Center the controls horizontally
        //ImGui.SetCursorPosX((availableWidth - 400) * 0.5f);

        
        if (_selectedAction != UserActions.None)
        {
            ImGui.Text($"Editing: {CustomComponents.HumanReadablePascalCase(Enum.GetName(_selectedAction))}");

            // Input field for the shortcut
            ImGui.SetNextItemWidth(242);
            ImGui.InputText("##ShortcutInput", ref _editingShortcut, 256);

            // Action buttons
            if (ImGui.Button("Assign", new Vector2(80, 0)))
            {
                CurrentKeyMap.SetKeyboardShortcut(_selectedAction, _editingShortcut, KeyMapSwitching.CurrentKeymap.Name);
                _selectedAction = UserActions.None;
                _somethingChanged = true;
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(80, 0)))
            {
                _selectedAction = UserActions.None;
            }

            ImGui.SameLine();
            if (ImGui.Button("Clear", new Vector2(80, 0)))
            {
                _editingShortcut = string.Empty;
                CurrentKeyMap.ClearKeyboardShortcut(_selectedAction);
                _selectedAction = UserActions.None;
                _somethingChanged = true;
            }
        }
        else
        {
            // Show placeholder state
            ImGui.Text("No action selected");

            // Disabled input field with placeholder text
            ImGui.BeginDisabled();
            ImGui.SetNextItemWidth(242);
            var placeholderText = "Select an action in the list below";
            var dummyText = "";
            ImGui.InputTextWithHint("##ShortcutInput", placeholderText, ref dummyText, 256, ImGuiInputTextFlags.ReadOnly);
            ImGui.EndDisabled();

            // Disabled buttons
            //ImGui.SameLine();
            ImGui.BeginDisabled();
            ImGui.Button("Save", new Vector2(80, 0));
            ImGui.SameLine();
            ImGui.Button("Cancel", new Vector2(80, 0));
            ImGui.SameLine();
            ImGui.Button("Clear", new Vector2(80, 0));
            ImGui.EndDisabled();
        }

        ImGui.EndChild();
        ImGui.Spacing();

        // Draw the table of shortcuts (same as before)
        if (ImGui.BeginTable("Shortcuts", 2, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch, 0.65f);
            ImGui.TableSetupColumn("Shortcut", ImGuiTableColumnFlags.WidthStretch, 0.35f);

            foreach (var value in Enum.GetValues<UserActions>())
            {
                ImGui.TableNextRow();

                if (_selectedAction == value)
                {
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0,
                                          ImGui.GetColorU32(ImGuiCol.Header));
                }

                ImGui.TableSetColumnIndex(0);
                var actionName = CustomComponents.HumanReadablePascalCase(Enum.GetName(value));
                var shortcuts = KeyActionHandling.ListKeyboardShortcutsForAction(value, false);

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

    // private static KeyMapHandling.KeyBinding _currentKeyBinding = new();
    // private static KeyMapHandling.KeyBinding _currentKeyBindingWithoutChanges = new();
    public static readonly object Dummy = new();
    private static UserActions _selectedAction = UserActions.None;
    private static string _editingShortcut = "";
}