#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using T3.Core.Logging;
using T3.Core.Model;
using T3.Core.Operator.Slots;
using T3.Core.Resource;
using T3.Core.Utils;

namespace T3.Core.Operator;

public abstract class Instance :  IGuidPathContainer, IResourceConsumer
{
    public abstract Type Type { get; }

    public Guid SymbolChildId => SymbolChild!.Id;

    internal void InitializeSymbolChildInfo(Symbol.Child child, Symbol.Child? parent, IReadOnlyList<Guid> instancePath)
    {
        if(SymbolChild != null)
            throw new InvalidOperationException("Instance already has a symbol child");
        
        if (instancePath.Count > 1)
        {
            _parentPath = new Guid[instancePath.Count - 1];
            for (var i = 0; i < _parentPath.Length; i++)
            {
                _parentPath[i] = instancePath[i];
            }
        }
        
        InstancePath = instancePath;
        SymbolChild = child;
        _parentSymbolChild = parent;
        Children = new InstanceChildren(instancePath, child);
    }

    public Symbol.Child SymbolChild { get; private set; }

    private Guid[]? _parentPath;
    private Symbol.Child? _parentSymbolChild;
    public Instance? Parent
    {
        get
        {
            if (_parentSymbolChild == null)
            {
                return null;
            }
            
            if (_parentPath == null)
                throw new InvalidOperationException($"Parent path is not initialized. Did you call {nameof(InitializeSymbolChildInfo)}?");
            
            if(_parentSymbolChild.TryGetOrCreateInstance(_parentPath, out var parentInstance, out _))
            {
                return parentInstance;
            }
            Log.Error($"Could not find parent instance for {_parentSymbolChild} with path {_parentPath}");
            return null;
        }
    }
        
    SymbolPackage IResourceConsumer.Package => Symbol.SymbolPackage;
    public event Action<IResourceConsumer>? Disposing;

    public abstract Symbol Symbol { get; }

    private readonly List<ISlot> _outputs = [];
    public readonly IReadOnlyList<ISlot> Outputs;

    private readonly List<IInputSlot> _inputs = [];
    public readonly IReadOnlyList<IInputSlot> Inputs;

    private bool _hasDisposed;
    public bool IsDisposed => _hasDisposed;

    public IReadOnlyList<IResourcePackage> AvailableResourcePackages
    {
        get
        {
            GatherResourcePackages(this, ref _availableResourcePackages);
            return _availableResourcePackages;
        }
    }

    /// <summary>
    /// get input without GC allocations 
    /// </summary>
    public IInputSlot? GetInput(Guid guid)
    {
        //return Inputs.SingleOrDefault(input => input.Id == guid);
        var inputCount = Inputs.Count;
        for (var index = 0; index < inputCount; index++)
        {
            var i = Inputs[index];
            if (i.Id == guid)
                return i;
        }

        return null;
    }

    protected Instance()
    {
        Outputs = _outputs;
        Inputs = _inputs;
    }

    public InstanceChildren Children;

    ~Instance()
    {
        if (!_hasDisposed)
        {
            Log.Error($"Instance {this} was not disposed properly");
            // todo : do we want to invoke disposal here? could this prevent memory leaks or other shutdown/reload issues?
        }
    }

    internal void Dispose(SymbolPackage? packageToDispose, int hash = -1)
    {
        if (packageToDispose != null && Symbol.SymbolPackage != packageToDispose)
        {
            // clear connections - they need to be repopulated with valid connections next time
            
            if(SymbolChild.TryGetOrCreateInstance(_parentPath!, out var parent, out _, false))
            {
                if (parent.IsDisposed)
                {
                    // clear our connections - we may be reassigned to another parent
                    for (var index = 0; index < _inputs.Count; index++)
                    {
                        var input = _inputs[index];
                        while (input.HasInputConnections)
                            input.RemoveConnection();
                    }
                }
            }
            
            // note this early return requires that no circular dependencies exist
            // (e.g. package A has an op from package B, which contains an op from package A - big no-no)
            // id love to keep this here but i know that it will be an issue since it is not currently enforced as far as i know
            // so we're gonna dispose our children anyway before we return
            // the goal is definitely to not need to though
            Children.Dispose(packageToDispose);
            return;
        }
        
        if (_hasDisposed)
        {
            throw new Exception($"{this} has already been disposed\n" + Environment.StackTrace);
        }
        
        _hasDisposed = true;

        try
        {
            Disposing?.Invoke(this);
        }
        catch (Exception e)
        {
            Log.Error($"Error on dispose event for {this}: {e}");
        }

        // kill children
        Children.Dispose(packageToDispose);

        try
        {
            Dispose(true);
        }
        catch (Exception e)
        {
            Log.Error($"Error disposing instance {this}: {e}");
        }

        SymbolChild.RemoveDisposedInstance(this, hash);
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    private protected void SetupInputAndOutputsFromType()
    {
        var symbol = Symbol;
        var assemblyInfo = symbol.SymbolPackage.AssemblyInformation;
        if (!assemblyInfo.OperatorTypeInfo.TryGetValue(symbol.Id, out var operatorTypeInfo))
        {
            Log.Error($"Can't find operatorTypeInfo for id {symbol} {symbol.Id} in {assemblyInfo}");
            Debug.Assert(false);
            
        }
        
        //var operatorTypeInfo = assemblyInfo.OperatorTypeInfo[symbol.Id];
        foreach (var input in operatorTypeInfo.Inputs)
        {
            var attribute = input.Attribute;
            var inputSlot = input.GetSlotObject(this);
            inputSlot.Parent = this;
            inputSlot.Id = attribute.Id;
            inputSlot.MappedType = attribute.MappedType;
            _inputs.Add(inputSlot);
        }

        // outputs identified by attribute
        foreach (var output in operatorTypeInfo.Outputs)
        {
            var slot = output.GetSlotObject(this);
            slot.Parent = this;
            slot.Id = output.Attribute.Id;
            _outputs.Add(slot);
        }
    }

    internal bool TryAddConnection(Symbol.Connection connection, int multiInputIndex)
    {
        if (!TryGetSourceSlot(connection, out var sourceSlot) || 
            !TryGetTargetSlot(connection, out var targetSlot))
            return false;

        targetSlot.AddConnection(sourceSlot, multiInputIndex);
        sourceSlot.DirtyFlag.Invalidate();
        return true;
    }
    
    private bool TryGetSourceSlot(Symbol.Connection connection, [NotNullWhen(true)] out ISlot? sourceSlot)
    {
        // Get source Instance
        IEnumerable<ISlot> sourceSlotList;
            
        var sourceParentOrChildId = connection.SourceParentOrChildId;
        if (sourceParentOrChildId == Guid.Empty)
        {
            sourceSlotList = Inputs;
        }
        else
        {
            if (!Children.TryGetChildInstance(sourceParentOrChildId, out var sourceInstance))
            {
                sourceSlot = null;
                return false;
            }

            sourceSlotList = sourceInstance.Outputs;
        }

        // Get source Slot
        sourceSlot = null;
        var gotSourceSlot = false;
            
        foreach(var slot in sourceSlotList)
        {
            if (slot.Id != connection.SourceSlotId)
                continue;
                
            sourceSlot = slot;
            gotSourceSlot = true;
            break;
        }
            
        return gotSourceSlot;
    }

    internal bool TryGetTargetSlot(Symbol.Connection connection, [NotNullWhen(true)] out ISlot? targetSlot)
    {
        // Get target Instance
        var targetParentOrChildId = connection.TargetParentOrChildId;
        IEnumerable<ISlot> targetSlotList;

        if (targetParentOrChildId == Guid.Empty)
        {
            targetSlotList = Outputs;
        }
        else
        {
            if (!Children.TryGetChildInstance(targetParentOrChildId, out var targetInstance))
            {
                targetSlot = null;
                return false;
            }
            
            targetSlotList = targetInstance.Inputs;
        }

        var gotTargetSlot = false;
        foreach(var slot in targetSlotList)
        {
            if (slot.Id != connection.TargetSlotId)
                continue;
                
            targetSlot = slot;
            return true;
        }

        targetSlot = null;
        return gotTargetSlot;
    }
    
   

    private static void GatherResourcePackages(Instance? instance, ref List<SymbolPackage> resourceFolders)
    {
        if (instance == null || !instance._resourceFoldersDirty)
            return;
            
        instance._resourceFoldersDirty = false;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (resourceFolders != null)
        {
            resourceFolders.Clear();
        }
        else
        {
            resourceFolders = [];
        }

        while (instance != null)
        {
            var package = instance.Symbol.SymbolPackage;
            if (!resourceFolders.Contains(package))
            {
                resourceFolders.Add(package);
            }

            instance = instance.Parent;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected internal bool TryGetFilePath(string relativePath, out string absolutePath, bool isFolder = false)
    {
        return ResourceManager.TryResolvePath(relativePath, this, out absolutePath, out _, isFolder);
    }
        
        
    internal static void SortInputSlotsByDefinitionOrder(Instance instance)
    {
        // order the inputs by the given input definitions. original order is coming from code, but input def order is the relevant one
        var inputs = instance._inputs;
        var inputDefinitions = instance.Symbol.InputDefinitions;
        int numInputs = inputs.Count;
        var lastIndex = numInputs - 1;

        for (int i = 0; i < lastIndex; i++)
        {
            Guid inputId = inputDefinitions[i].Id;
            if (inputs[i].Id != inputId)
            {
                int index = inputs.FindIndex(i + 1, input => input.Id == inputId);
                if (index == -1)
                    continue;
                //Debug.Assert(index >= 0);
                inputs.Swap(i, index);
                Debug.Assert(inputId == inputs[i].Id);
            }
        }

        #if DEBUG
            if (numInputs > 0)
            {
#if SKIP_ASSERTS
                Debug.Assert(inputs.Count == inputDefinitions.Count);
#endif
            }
        #endif
    }

    public IReadOnlyList<Guid> InstancePath { get; private set; }

    private List<SymbolPackage> _availableResourcePackages = [];
    private bool _resourceFoldersDirty = true;

    public sealed override string ToString()
    {
        const string fmt = "{0} ({1})";
        return _asString ??= string.Format(fmt, GetType().Name, SymbolChildId.ToString());
    }

    private string? _asString;

    internal void MarkResourceDirectoriesDirty()
    {
        _resourceFoldersDirty = true;
    }
}

public class Instance<T> : Instance where T : Instance
{
    // this intended to be a different symbol per-type
    // this is set via reflection in the Symbol ApplyInstanceType method
    // ReSharper disable once StaticMemberInGenericType
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once FieldCanBeMadeReadOnly.Global
    private protected static Symbol StaticSymbol = null!;
        
    public sealed override Type Type => typeof(T);
    public sealed override Symbol Symbol => StaticSymbol;

    protected Instance()
    {
        SetupInputAndOutputsFromType();
    }
}