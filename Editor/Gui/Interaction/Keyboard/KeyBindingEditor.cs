#nullable enable
using System.Windows.Forms;
using ImGuiNET;
using Silk.NET.GLFW;
using T3.Core.UserData;
using T3.Editor.Gui.Styling;
using T3.Editor.Gui.UiHelpers;
using T3.SystemUi;

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
                                   "Key Map",
                                   $"Edit the shortcuts as you wish you'll find the {FileLocations.SettingsPath}."))
        {
            if (KeyMapSwitching.TrySetKeyMap(userSelection))
            {
                UserSettings.Config.KeyBindingName = userSelection;
                UserSettings.Save();

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
        if (ImGui.Button("Clone"))
        {
            KeyMapSwitching.CloneCurrentKeymap();
            UserSettings.Config.KeyBindingName = CurrentKeyMap.Name;
            UserSettings.Save();
            _somethingChanged = true;
        }

        if (CurrentKeyMap.ReadOnly)
        {
            if (CustomComponents.DisablableButton("Save", _somethingChanged))
            {
                // TODO: and check if saving worked
                //KeyBindingHandling.SaveKeyBinding(_currentKeyBinding);
                KeyMapSwitching.SaveKeyMap(CurrentKeyMap);
                _currentKeyBindingWithoutChanges = CurrentKeyMap.Clone();

                UserSettings.Config.KeyBindingName = KeyMapSwitching.CurrentKeymap.Name;
            }
        }
        
        FormInputs.AddVerticalSpace();
        ImGui.Separator();
        FormInputs.AddVerticalSpace();
        DrawKeybindingEdits();
    }

    private static void DrawKeybindingEdits()
    {
        var needUpdate = false;
        
        // Draw the table of shortcuts (same as before)
        if (ImGui.BeginTable("Shortcuts", 2, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch, 0.65f);
            ImGui.TableSetupColumn("Shortcut", ImGuiTableColumnFlags.WidthStretch, 0.35f);

            foreach (var action in Enum.GetValues<UserActions>())
            {
                if (action == UserActions.__Count)
                    continue;

                ImGui.TableNextRow();

                if (_selectedAction == action)
                {
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0,
                                          ImGui.GetColorU32(ImGuiCol.Header));
                }

                ImGui.TableSetColumnIndex(0);
                var actionName = CustomComponents.HumanReadablePascalCase(Enum.GetName(action));
                var shortcuts = action.ListKeyboardShortcutsForAction(false);

                if (ImGui.Selectable(actionName, _selectedAction == action,
                                     ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap))
                {
                    _selectedAction = action;
                    CurrentKeyMap.TryGetBinding(action, out var keyBinding);
                    _selectedCombo = keyBinding.KeyCombination;
                    ImGui.OpenPopup(actionName);
                }

                ImGui.TableSetColumnIndex(1);

                if (!string.IsNullOrEmpty(shortcuts))
                {
                    ImGui.PushFont(Fonts.FontBold);
                    ImGui.TextUnformatted(shortcuts);
                    ImGui.PopFont();
                }

                needUpdate |= DrawEditPopUp(action, actionName);
            }

            ImGui.EndTable();
        }

        if (needUpdate)
        {
            _somethingChanged = true;
            CurrentKeyMap.UpdateShortcutLabels();
        }
    }

    private static KeyCombination _selectedCombo;

    private static bool DrawEditPopUp(UserActions action, string actionName)
    {
        if (!ImGui.BeginPopup(actionName))
            return false;

        var needsUpdate = false;

        if (DrawToggle("Ctrl", _selectedCombo.Ctrl))
            _selectedCombo.Ctrl = !_selectedCombo.Ctrl;

        ImGui.SameLine(0, 4);
        if (DrawToggle("Alt", _selectedCombo.Alt))
            _selectedCombo.Alt = !_selectedCombo.Alt;

        ImGui.SameLine(0, 4);

        if (DrawToggle("Shift", _selectedCombo.Shift))
            _selectedCombo.Shift = !_selectedCombo.Shift;

        ImGui.SameLine(0, 10);


        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            ImGui.CloseCurrentPopup();    
        }
        
        var pressedKey = KeyHandler.GetPressedKey();
        if (pressedKey != Key.Undefined)
        {
            _selectedCombo.Key = pressedKey;
        }
        
        var k = _selectedCombo.Key;
        
        ImGui.SetNextItemWidth(100);
        if (FormInputs.DrawEnumDropdown(ref k, "key"))
            _selectedCombo.Key = k;

        FormInputs.AddVerticalSpace(5);
        
        if (CurrentKeyMap.TryGetConflictingBinding(_selectedAction, _selectedCombo, out var conflictingBinding))
        {
            CustomComponents.StylizedText($"Already used for {conflictingBinding.Action}", Fonts.FontSmall, UiColors.StatusAttention);
        }
        FormInputs.AddVerticalSpace();
        
        // Action buttons
        var isAssignValid = _selectedCombo.Key != Key.Undefined
                            && !CurrentKeyMap.DoesBindingMatchCombo(_selectedAction, _selectedCombo);
        if (CustomComponents.DisablableButton("Assign", isAssignValid))
        {
            CurrentKeyMap.AddBinding(_selectedAction, _selectedCombo);
            ImGui.CloseCurrentPopup();
            needsUpdate = true;
        }

        ImGui.SameLine();

        var isRemoveValid = _selectedCombo.Key != Key.Undefined && _selectedAction != UserActions.None;
        if (CustomComponents.DisablableButton("Remove", isRemoveValid))
        {
            CurrentKeyMap.RemoveBinding(_selectedAction);
            _selectedAction = UserActions.None;
            ImGui.CloseCurrentPopup();
            needsUpdate = true;
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            ImGui.CloseCurrentPopup();
        }

        FormInputs.AddVerticalSpace();

        ImGui.EndPopup();

        return needsUpdate;
    }

    private static bool DrawToggle(string label, bool enabled)
    {
        return CustomComponents.ToggleButton(label, ref enabled, new Vector2(40, 0));
    }

    private static bool _somethingChanged;

    private static bool _initialized;

    public static readonly object Dummy = new();
    private static UserActions _selectedAction = UserActions.None;
}