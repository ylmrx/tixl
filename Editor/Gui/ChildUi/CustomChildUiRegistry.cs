#nullable enable
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using ImGuiNET;
using T3.Core.Operator;
using T3.Core.Utils;
using T3.Editor.Gui.Graph.CustomUi;
using T3.Editor.Gui.UiHelpers;
using T3.Editor.UiModel;

namespace T3.Editor.Gui.ChildUi;

// public static class CustomChildUiRegistry
// {
//     private static readonly ConcurrentDictionary<Guid, DrawChildUiDelegate> EntriesRw = new();
//
//     public static void Register(Guid symbolId, DrawChildUiDelegate drawChildUiDelegate, ICollection<Guid> types)
//     {
//         if (EntriesRw.TryAdd(symbolId, drawChildUiDelegate))
//         {
//             types.Add(symbolId);
//             Log.Debug("Registered custom child UI for type: " + symbolId);
//         }
//     }
//
//     internal static bool TryGetValue(Guid symbolId, [NotNullWhen(true)] out DrawChildUiDelegate? o)
//     {
//         return EntriesRw.TryGetValue(symbolId, out o);
//     }
//
//     public static bool Remove(Guid symbolId, ICollection<Guid> types)
//     {
//         if (EntriesRw.TryRemove(symbolId, out _))
//         {
//             types.Remove(symbolId);
//             return true;
//         }
//
//         return false;
//     }
// }

