#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using T3.Core.Logging;
using T3.Core.Model;

namespace T3.Core.Operator;

public sealed class InstanceChildren : IEnumerable<(Guid, Instance)>
{
    private readonly Symbol.Child _asChild;
    private readonly Guid[] _childSearchPath;
    
    public int Count => _asChild.Symbol.Children.Count;
    
    internal InstanceChildren(IReadOnlyList<Guid> instancePath, Symbol.Child symbolChild)
    {
        _asChild = symbolChild;
        _childSearchPath = new Guid[instancePath.Count + 1];
        for (var i = 0; i < instancePath.Count; i++)
        {
            _childSearchPath[i] = instancePath[i];
        }
        _childSearchPath[^1] = Guid.Empty;
    }
    
    // array accessor overload
    public Instance this[Guid sourceParentOrChildId]
    {
        get
        {
            if (TryGetChildInstance(sourceParentOrChildId, out var sourceInstance))
                return sourceInstance;

            throw new KeyNotFoundException($"Child with id {sourceParentOrChildId} not found in {this}");
        }
    }
    
    public IEnumerable<Instance> Values 
    {
        get
        {
            foreach (var child in _asChild.Symbol.Children.Values)
            {
                if (TryGetChildInstance(child.Id, out var instance))
                {
                    yield return instance;
                }
                else
                {
                    Log.Error($"Child {child.Id} not found in {this}");
                }
            }
        }
    }

    public bool TryGetChildInstance(Guid childId, [NotNullWhen(true)] out Instance? instance, bool allowCreate = true)
    {
        var symbol = _asChild.Symbol;
        if (!symbol.Children.TryGetValue(childId, out var sourceChild))
        {
            Log.Error($"{_asChild} failed to find child : {childId}");
            instance = null;
            return false;
        }

        lock (_childSearchPath)
        {
            _childSearchPath[^1] = childId;
            if (!sourceChild.TryGetOrCreateInstance(_childSearchPath, out instance, out _, allowCreate))
            {
                if(allowCreate)
                    Log.Error($"{_asChild} Failed to find child instance : {childId}");
                return false;
            }
        }

        return true;
    }

    internal void Dispose(SymbolPackage? packageToDispose)
    {
        foreach (var child in _asChild.Symbol.Children.Values)
        {
            if(packageToDispose != null && child.Symbol.SymbolPackage != packageToDispose)
                continue;
            
            var childId = child.Id;
            lock (_childSearchPath)
            {
                _childSearchPath[^1] = childId;
                _asChild.DisposeAndRemoveIfExists(_childSearchPath, packageToDispose);
            }
        }
    }

    public IEnumerator<(Guid, Instance)> GetEnumerator()
    {
        foreach (var child in _asChild.Symbol.Children)
        {
            if (TryGetChildInstance(child.Key, out var instance))
            {
                yield return (child.Key, instance);
            }
            else
            {
                Log.Error($"Child {child.Key} not found in {this}");
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}