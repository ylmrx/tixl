#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ImGuiNET;
using T3.Core.Operator;
using T3.Core.Operator.Slots;
using T3.Editor.Gui.OpUis.UIs;
using T3.Editor.Gui.UiHelpers;

namespace T3.Editor.Gui.OpUis;

internal delegate OpUi.CustomUiResult DrawChildUiDelegate(Instance instance,
                                                          ImDrawListPtr drawList,
                                                          ImRect area,
                                                          Vector2 scale,
                                                          ref CustomUiParamSet? data);

/// <summary>
/// CustomUi are interfaces rendered within the graph interface operators.
/// This class provides some helpers methods to help accessing the operator instances input fields.
/// </summary>
public static class OpUi
{
    internal static OpUi.CustomUiResult DrawCustomUi(this Instance instance, ImDrawListPtr drawList, ImRect selectableScreenRect, Vector2 canvasScale)
    {
        if (!_drawFunctionsForSymbolIds.TryGetValue(instance.Symbol.Id, out var drawFunction))
            return OpUi.CustomUiResult.None;

        if (!ImGui.IsRectVisible(selectableScreenRect.Min, selectableScreenRect.Max))
            return OpUi.CustomUiResult.None;

        // Unfortunately we have to test if symbolChild of instance is still valid.
        // This might not be the case for operations like undo/redo.
        if (instance.IsDisposed || instance.Parent == null || !instance.Parent.Children.TryGetChildInstance(instance.SymbolChildId, out _))
            return OpUi.CustomUiResult.None;

        CustomUiParamSet? data = null;
        return drawFunction(instance, drawList, selectableScreenRect, canvasScale, ref data);
    }

    /// <summary>
    /// Each custom op needs to collect the input slots for its parameters
    /// </summary>
    internal static bool TryGetInput<T>(Instance instance, Guid id, [NotNullWhen(true)] out InputSlot<T>? slot)
    {
        var someSlot = instance.Inputs.FirstOrDefault(i => i.Id == id);
        if (someSlot is not InputSlot<T> matchingSlot)
        {
            Log.Warning($"Can't find input slot {id} in {instance} to draw custom ui");
            slot = null;
            return false;
        }

        slot = matchingSlot;
        return true;
    }

    /// <summary>
    /// Each custom op needs to collect the input slots for its parameters
    /// </summary>
    internal static bool TryGetOutput<T>(Instance instance, Guid id, [NotNullWhen(true)] out Slot<T>? slot)
    {
        var someSlot = instance.Outputs.FirstOrDefault(i => i.Id == id);
        if (someSlot is not Slot<T> matchingSlot)
        {
            Log.Warning($"Can't find output slot {id} in {instance} to draw custom ui");
            slot = null;
            return false;
        }

        slot = matchingSlot;
        return true;
    }

    /// <summary>
    /// For certain edge-cases the opUi needs access to public field of the instance.
    /// Referencing those by name is unfortunate but allows decoupling the type definition from the ui.
    /// </summary>
    internal static bool TryGetField(Instance instance, string name, [NotNullWhen(true)] out FieldInfo? fieldInfo)
    {
        fieldInfo = instance.GetType().GetField(name);
        if (fieldInfo == null)
        {
            Log.Warning($"Can't find field '{name}' in {instance} to draw custom ui");
        }
        return fieldInfo != null;
    }

    /// <summary>
    /// For certain edge-cases the opUi needs access to public field of the instance.
    /// Referencing those by name is unfortunate but allows decoupling the type definition from the ui.
    /// </summary>
    internal static bool TryGetProperty(Instance instance, string name, [NotNullWhen(true)] out PropertyInfo? propInfo)
    {
        propInfo = instance.GetType().GetProperty(name);
        if (propInfo == null)
        {
            Log.Warning($"Can't find property slot '{name}' in {instance} to draw custom ui");
        }
        return propInfo != null;
    }

    /// <summary>
    /// Results return when drawing custom UIs
    /// </summary>
    [Flags]
    public enum CustomUiResult
    {
        None = 0,
        Rendered = 1 << 2,
        IsActive = 1 << 3,
        PreventTooltip = 1 << 4,
        PreventOpenSubGraph = 1 << 5,
        PreventOpenParameterPopUp = 1 << 6,
        PreventInputLabels = 1 << 7,
    }

    /// <remarks>
    /// Having this list repeated here is unfortunate, but other attempts of automatically
    /// registration would either require non-static classes or reflection.
    /// </remarks>
    private static readonly Dictionary<Guid, DrawChildUiDelegate> _drawFunctionsForSymbolIds
        = new()
              {
                  { Guid.Parse("11882635-4757-4cac-a024-70bb4e8b504c"), CounterUi.DrawChildUi },
                  { Guid.Parse("8211249d-7a26-4ad0-8d84-56da72a5c536"), GradientSliderUi.DrawChildUi },
                  { Guid.Parse("23794a1f-372d-484b-ac31-9470d0e77819"), Jitter2dUi.DrawChildUi },
                  { Guid.Parse("ea7b8491-2f8e-4add-b0b1-fd068ccfed0d"), AnimValueUi.DrawChildUi },

                  // { Guid.Parse("b724ea74-d5d7-4928-9cd1-7a7850e4e179"), SampleCurveUi.DrawChildUi },
                  // { Guid.Parse("3b0eb327-6ad8-424f-bca7-ccbfa2c9a986"), _JitterUi.DrawChildUi },
                  // { Guid.Parse("5880cbc3-a541-4484-a06a-0e6f77cdbe8e"), StringUi.DrawChildUi },
                  // { Guid.Parse("5d7d61ae-0a41-4ffa-a51d-93bab665e7fe"), ValueUi.DrawChildUi },
                  // { Guid.Parse("cc07b314-4582-4c2c-84b8-bb32f59fc09b"), IntValueUi.DrawChildUi },
                  // { Guid.Parse("f0acd1a4-7a98-43ab-a807-6d1bd3e92169"), RemapUi.DrawChildUi },
                  // { Guid.Parse("af79ee8c-d08d-4dca-b478-b4542ed69ad8"), AnimVec2Ui.DrawChildUi },
                  // { Guid.Parse("7814fd81-b8d0-4edf-b828-5165f5657344"), AnimVec3Ui.DrawChildUi },
                  // { Guid.Parse("94a392e6-3e03-4ccf-a114-e6fafa263b4f"), SequenceAnimUi.DrawChildUi },
                  // { Guid.Parse("95d586a2-ee14-4ff5-a5bb-40c497efde95"), TriggerAnimUi.DrawChildUi },
                  // { Guid.Parse("59a0458e-2f3a-4856-96cd-32936f783cc5"), MidiInputUi.DrawChildUi },
                  // { Guid.Parse("ed0f5188-8888-453e-8db4-20d87d18e9f4"), BooleanUi.DrawChildUi },
                  // { Guid.Parse("0bec016a-5e1b-467a-8273-368d4d6b9935"), TriggerUi.DrawChildUi },
                  //
                  // { Guid.Parse("470db771-c7f2-4c52-8897-d3a9b9fc6a4e"), GetIntVarUi.DrawChildUi },
                  // { Guid.Parse("e6072ecf-30d2-4c52-afa1-3b195d61617b"), GetFloatVarUi.DrawChildUi },
                  // { Guid.Parse("2a0c932a-eb81-4a7d-aeac-836a23b0b789"), SetFloatVarUi.DrawChildUi },
                  //
                  // { Guid.Parse("03477b9a-860e-4887-81c3-5fe51621122c"), AudioReactionUi.DrawChildUi },
                  // { Guid.Parse("000e08d0-669f-48df-9083-7aa0a43bbc05"), GpuMeasureUi.DrawChildUi },
                  // { Guid.Parse("bfe540ef-f8ad-45a2-b557-cd419d9c8e44"), DataListUi.DrawChildUi },
                  //
                  // { Guid.Parse("96b1e8f3-0b42-4a01-b82b-44ccbd857400"), SelectVec2FromDictUi.DrawChildUi },
                  // { Guid.Parse("05295c65-7dfd-4570-866e-9b5c4e735569"), SelectBoolFromFloatDictUi.DrawChildUi },
              };
}

/// <summary>
/// This abstract class will later allow caching of the parameter input references.
/// </summary>
internal abstract class CustomUiParamSet
{
    public bool IsValid;
}