#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using T3.Core.Compilation;
using T3.Core.DataTypes;
using T3.Core.Logging;
using T3.Core.Model;
using T3.Core.Operator.Slots;
using Texture2D = T3.Core.DataTypes.Texture2D;

namespace T3.Core.Operator;

public partial class Symbol
{
    /// <summary>
    /// Represents an instance of a <see cref="Symbol"/> within a Symbol.
    /// </summary>
    public sealed class Child
    {
        /// <summary>A reference to the <see cref="Symbol"/> this is an instance from.</summary>
        public Symbol Symbol { get; init; }

        public Guid Id { get; }

        public Symbol? Parent { get; }

        public string Name { get; set; }

        public string ReadableName => string.IsNullOrEmpty(Name) ? Symbol.Name : Name;
        public bool HasCustomName => !string.IsNullOrEmpty(Name);

        public bool IsBypassed { get => _isBypassed; set => SetBypassed(value); }

        public bool IsDisabled
        {
            get
            {
                // Avoid LINQ because of allocations in inner loop
                foreach (var x in Outputs.Values)
                {
                    if (x.IsDisabled)
                        return true;
                }

                return false;
                //return Outputs.FirstOrDefault().Value?.IsDisabled ?? false;
            }
            set => SetDisabled(value);
        }

        public Dictionary<Guid, Input> Inputs { get; private init; } = new();
        public Dictionary<Guid, Output> Outputs { get; private init; } = new(); 
        internal IEnumerable<Instance> Instances
        {
            get
            {
                lock(_creationLock)
                    return _instancesOfSelf.Values;
            }
        }

        private readonly Dictionary<int, Instance> _instancesOfSelf = [];
        private readonly object _creationLock;
        private readonly bool _isGeneric;

        public Guid? PreviousId { get; private set; }


        internal Child(Symbol symbol, Guid childId, Symbol? parent, string? name, bool isBypassed, object creationLock, Guid? previousId = null)
        {
            _creationLock = creationLock;
            Symbol = symbol;
            Id = childId;
            Parent = parent;
            Name = name ?? string.Empty;
            _isBypassed = isBypassed;
            _isGeneric = symbol.IsGeneric;
            PreviousId = previousId;

            foreach (var inputDefinition in symbol.InputDefinitions)
            {
                if (!Inputs.TryAdd(inputDefinition.Id, new Input(inputDefinition)))
                {
                    throw new ApplicationException($"The ID for symbol input {symbol.Name}.{inputDefinition.Name} must be unique.");
                }
            }

            foreach (var outputDefinition in symbol.OutputDefinitions)
            {
                Symbol.OutputDefinition.TryGetNewOutputDataType(outputDefinition, out var outputData);
                var output = new Output(outputDefinition, outputData) { DirtyFlagTrigger = outputDefinition.DirtyFlagTrigger };
                if (!Outputs.TryAdd(outputDefinition.Id, output))
                {
                    throw new ApplicationException($"The ID for symbol output {symbol.Name}.{outputDefinition.Name} must be unique.");
                }
            }
        }

        private void SetDisabled(bool shouldBeDisabled)
        {
            if (Parent == null)
                return;

            var outputDefinitions = Symbol.OutputDefinitions;

            // Set disabled status on this child's outputs
            foreach (var outputDef in outputDefinitions)
            {
                if (outputDef == null)
                {
                    Log.Warning($"{Symbol.GetType()} {Symbol.Name} contains a null {typeof(Symbol.OutputDefinition)}", Id);
                    continue;
                }

                if (Outputs.TryGetValue(outputDef.Id, out var childOutput))
                {
                    childOutput.IsDisabled = shouldBeDisabled;

                }
                else
                {
                    Log.Warning($"{typeof(Symbol.Child)} {ReadableName} does not have the following child output as defined: " +
                                $"{childOutput?.OutputDefinition.Name}({nameof(Guid)}{childOutput?.OutputDefinition.Id})");
                }
            }

            // Set disabled status on outputs of each instanced copy of this child within all parents that contain it
            foreach (var parentInstance in Parent.InstancesOfSelf)
            {
                // This parent doesn't have an instance of our SymbolChild. Ignoring and continuing.
                if (!parentInstance.Children.TryGetChildInstance(Id, out var matchingChildInstance))
                    continue;

                // Set disabled status on all outputs of each instance
                foreach (var slot in matchingChildInstance.Outputs)
                {
                    slot.IsDisabled = shouldBeDisabled;
                }
            }
        }

        #region sub classes =============================================================
        public sealed class Output
        {
            public Symbol.OutputDefinition OutputDefinition { get; }
            public IOutputData OutputData { get; }

            public bool IsDisabled { get; set; }

            public DirtyFlagTrigger DirtyFlagTrigger
            {
                get => _dirtyFlagTrigger ?? OutputDefinition.DirtyFlagTrigger;
                set => _dirtyFlagTrigger = (value != OutputDefinition.DirtyFlagTrigger) ? (DirtyFlagTrigger?)value : null;
            }

            private DirtyFlagTrigger? _dirtyFlagTrigger = null;

            public Output(Symbol.OutputDefinition outputDefinition, IOutputData outputData)
            {
                OutputDefinition = outputDefinition;
                OutputData = outputData;
            }

            public Output DeepCopy()
            {
                return new Output(OutputDefinition, OutputData);
            }
        }

        public sealed class Input
        {
            public Symbol.InputDefinition InputDefinition { get; }
            public Guid Id => InputDefinition.Id;
            public bool IsMultiInput => InputDefinition.IsMultiInput;
            public InputValue DefaultValue => InputDefinition.DefaultValue;

            public string Name => InputDefinition.Name;

            /// <summary>The input value used for this symbol child</summary>
            public InputValue Value { get; }

            public bool IsDefault { get; set; }

            public Input(Symbol.InputDefinition inputDefinition)
            {
                InputDefinition = inputDefinition;
                Value = DefaultValue.Clone();
                IsDefault = true;
            }

            public void SetCurrentValueAsDefault()
            {
                if (DefaultValue.IsEditableInputReferenceType)
                {
                    DefaultValue.AssignClone(Value);
                }
                else
                {
                    DefaultValue.Assign(Value);
                }

                IsDefault = true;
            }

            public void ResetToDefault()
            {
                if (DefaultValue.IsEditableInputReferenceType)
                {
                    Value.AssignClone(DefaultValue);
                }
                else
                {
                    Value.Assign(DefaultValue);
                }

                IsDefault = true;
            }
        }
        #endregion

        private bool _isBypassed;

        public bool IsBypassable()
        {
            if (Symbol.OutputDefinitions.Count == 0)
                return false;

            if (Symbol.InputDefinitions.Count == 0)
                return false;

            var mainInput = Symbol.InputDefinitions[0];
            var mainOutput = Symbol.OutputDefinitions[0];

            if (mainInput.DefaultValue.ValueType != mainOutput.ValueType)
                return false;

            if (mainInput.DefaultValue.ValueType == typeof(Command))
                return true;

            if (mainInput.DefaultValue.ValueType == typeof(Texture2D))
                return true;

            if (mainInput.DefaultValue.ValueType == typeof(BufferWithViews))
                return true;

            if (mainInput.DefaultValue.ValueType == typeof(MeshBuffers))
                return true;

            if (mainInput.DefaultValue.ValueType == typeof(float))
                return true;

            if (mainInput.DefaultValue.ValueType == typeof(Vector2))
                return true;

            if (mainInput.DefaultValue.ValueType == typeof(Vector3))
                return true;

            if (mainInput.DefaultValue.ValueType == typeof(string))
                return true;

            if (mainInput.DefaultValue.ValueType == typeof(ShaderGraphNode))
                return true;

            return false;
        }

        private void SetBypassed(bool shouldBypass)
        {
            if (shouldBypass == _isBypassed)
                return;

            if (!IsBypassable())
                return;

            if (Parent == null)
            {
                // Clarify: shouldn't this be shouldBypass?
                _isBypassed = shouldBypass; // during loading parents are not yet assigned. This flag will later be used when creating instances
                return;
            }

            lock (_creationLock)
            {
                if (_instancesOfSelf.Count == 0)
                {
                    _isBypassed = shouldBypass; // while duplicating / cloning as new symbol there are no instances yet.
                    return;
                }
            }

            // check if there is a connection
            var isOutputConnected = false;
            var mainOutputDef = Symbol.OutputDefinitions[0];
            foreach (var connection in Parent.Connections)
            {
                if (connection.SourceSlotId != mainOutputDef.Id || connection.SourceParentOrChildId != Id)
                    continue;

                isOutputConnected = true;
                break;
            }

            if (!isOutputConnected)
                return;

            var id = Id;
            foreach (var parentInstance in Parent.InstancesOfSelf)
            {
                var instance = parentInstance.Children[id];
                SetBypassForInstance(instance, shouldBypass);
            }

            _isBypassed = shouldBypass;
        }

        private static bool SetBypassForInstance(Instance instance, bool shouldBypass, bool invalidate = true)
        {
            var mainInputSlot = instance.Inputs[0];
            var mainOutputSlot = instance.Outputs[0];

            var wasByPassed = false;

            switch (mainOutputSlot)
            {
                case Slot<Command> commandOutput when mainInputSlot is Slot<Command> commandInput:
                    if (shouldBypass)
                    {
                        wasByPassed = commandOutput.TrySetBypassToInput(commandInput);
                    }
                    else
                    {
                        commandOutput.RestoreUpdateAction();
                    }

                    if (invalidate)
                        InvalidateConnected(commandInput);
                    break;

                case Slot<BufferWithViews> bufferOutput when mainInputSlot is Slot<BufferWithViews> bufferInput:
                    if (shouldBypass)
                    {
                        wasByPassed = bufferOutput.TrySetBypassToInput(bufferInput);
                    }
                    else
                    {
                        bufferOutput.RestoreUpdateAction();
                    }

                    if (invalidate)
                        InvalidateConnected(bufferInput);
                    
                    break;
                case Slot<MeshBuffers> bufferOutput when mainInputSlot is Slot<MeshBuffers> bufferInput:
                    if (shouldBypass)
                    {
                        wasByPassed = bufferOutput.TrySetBypassToInput(bufferInput);
                    }
                    else
                    {
                        bufferOutput.RestoreUpdateAction();
                    }

                    if (invalidate)
                        InvalidateConnected(bufferInput);

                    break;
                case Slot<Texture2D> texture2dOutput when mainInputSlot is Slot<Texture2D> texture2dInput:
                    if (shouldBypass)
                    {
                        wasByPassed = texture2dOutput.TrySetBypassToInput(texture2dInput);
                    }
                    else
                    {
                        texture2dOutput.RestoreUpdateAction();
                    }

                    if (invalidate)
                        InvalidateConnected(texture2dInput);

                    break;
                case Slot<float> floatOutput when mainInputSlot is Slot<float> floatInput:
                    if (shouldBypass)
                    {
                        wasByPassed = floatOutput.TrySetBypassToInput(floatInput);
                    }
                    else
                    {
                        floatOutput.RestoreUpdateAction();
                    }

                    if (invalidate)
                        InvalidateConnected(floatInput);

                    break;

                case Slot<System.Numerics.Vector2> vec2Output when mainInputSlot is Slot<System.Numerics.Vector2> vec2Input:
                    if (shouldBypass)
                    {
                        wasByPassed = vec2Output.TrySetBypassToInput(vec2Input);
                    }
                    else
                    {
                        vec2Output.RestoreUpdateAction();
                    }

                    if (invalidate)
                        InvalidateConnected(vec2Input);

                    break;
                case Slot<System.Numerics.Vector3> vec3Output when mainInputSlot is Slot<System.Numerics.Vector3> vec3Input:
                    if (shouldBypass)
                    {
                        wasByPassed = vec3Output.TrySetBypassToInput(vec3Input);
                    }
                    else
                    {
                        vec3Output.RestoreUpdateAction();
                    }

                    if (invalidate)
                        InvalidateConnected(vec3Input);

                    break;
                case Slot<string> stringOutput when mainInputSlot is Slot<string> stringInput:
                    if (shouldBypass)
                    {
                        wasByPassed = stringOutput.TrySetBypassToInput(stringInput);
                    }
                    else
                    {
                        stringOutput.RestoreUpdateAction();
                    }

                    if (invalidate)
                        InvalidateConnected(stringInput);
                    break;
            }

            return wasByPassed;
        }

        private static void InvalidateConnected<T>(Slot<T> bufferInput)
        {
            if (bufferInput.TryGetAsMultiInputTyped(out var multiInput))
            {
                foreach (var connection in multiInput.CollectedInputs)
                {
                    InvalidateParentInputs(connection);
                }
            }
            else
            {
                var connection = bufferInput.FirstConnection;
                InvalidateParentInputs(connection);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void InvalidateParentInputs(ISlot connection)
            {
                if (connection.ValueType == typeof(string))
                    return;

                connection.DirtyFlag.Invalidate();
            }
        }

        public override string ToString()
        {
            return Parent?.Name + ">" + ReadableName;
        }

        internal static Guid CreateIdDeterministically(Symbol symbol, Symbol? parent)
        {
            //deterministically create a new guid from the symbol id
            using var hashComputer = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            hashComputer.AppendData(symbol.Id.ToByteArray(), 0, 16);

            if (parent != null)
            {
                hashComputer.AppendData(parent.Id.ToByteArray(), 0, 16);
            }

            // SHA1 is 20 bytes long, but we only need 16 bytes for a guid
            var newGuidBytes = new ReadOnlySpan<byte>(hashComputer.GetHashAndReset(), 0, 16);
            return new Guid(newGuidBytes);
        }

        internal void RemoveChildInstancesOf(Child child)
        {
            var idToDestroy = child.Id;
            lock (_creationLock)
            {
                foreach (var instanceKvp in _instancesOfSelf)
                {
                    var instance = instanceKvp.Value;
                    if (instance.Children.TryGetChildInstance(idToDestroy, out var childInstance, false, false))
                    {
                        childInstance.Dispose(null);
                    }
                }
            }
        }

        private void DestroyAndClearAllInstances(SymbolPackage? onlyDisposeInPackage)
        {
            lock (_creationLock)
            {
                var allInstances = _instancesOfSelf.Values.ToArray();
                for (int i = allInstances.Length - 1; i >= 0; i--)
                {
                    allInstances[i].Dispose(onlyDisposeInPackage); // removes self from _instancesOfSelf dict
                }
                
                Debug.Assert(_instancesOfSelf.Count == 0, $"All instances of {Symbol.Name} should have been disposed, but {_instancesOfSelf.Count} remain.");
            }
        }

        internal void Dispose()
        {
            DestroyAndClearAllInstances(null);
            lock (_creationLock)
            {
                var removed = Symbol._childrenCreatedFromMe.Remove(Id, out _);
                Debug.Assert(removed);
            }
        }

        internal void AddChildInstances(Child newChild, ICollection<Instance> listToAddNewInstancesTo)
        {
            lock (_creationLock)
            {
                foreach (var instance in _instancesOfSelf.Values)
                {
                    var path = instance.InstancePath.Append(newChild.Id).ToArray();
                    if (newChild.TryGetOrCreateInstance(path, out var newInstance, out var created, true))
                    {
                        if (created)
                        {
                            listToAddNewInstancesTo.Add(newInstance);
                        }
                    }
                }
            }
        }

        internal bool UpdateIOAndConnections(SlotChangeInfo slotChanges)
        {
            UpdateSymbolChildIO(this, slotChanges);

            if (Parent == null)
            { 
                DestroyAndClearAllInstances(Symbol.SymbolPackage);
                // just destroy all instances - we have no connections to worry about since we dont have a parent
                return false;
            }

            // we dont need to update our instances/connections - our parents do that for us if they need it
            if (Parent.NeedsTypeUpdate && Parent.SymbolPackage == Symbol.SymbolPackage)
            {
                // destroy all instances if necessary? probably not...
                //DestroyAndClearAllInstances();
                return false;
            }

            // deal with removed connections
            var parentConnections = Parent!.Connections;
            // get all connections that belong to this instance
            var connectionsToReplace = parentConnections.FindAll(c => c.SourceParentOrChildId == Id ||
                                                                      c.TargetParentOrChildId == Id);

            // first remove those connections where the inputs/outputs doesn't exist anymore
            var connectionsToRemove =
                connectionsToReplace.FindAll(c =>
                                             {
                                                 return slotChanges.RemovedOutputDefinitions.Any(output =>
                                                                                                 {
                                                                                                     var outputId = output.Id;
                                                                                                     return outputId == c.SourceSlotId ||
                                                                                                         outputId == c.TargetSlotId;
                                                                                                 })
                                                        || slotChanges.RemovedInputDefinitions.Any(input =>
                                                                                                   {
                                                                                                       var inputId = input.Id;
                                                                                                       return inputId == c.SourceSlotId ||
                                                                                                           inputId == c.TargetSlotId;
                                                                                                   });
                                             });

            foreach (var connection in connectionsToRemove)
            {
                Parent.RemoveConnection(connection); // TODO: clarify if we need to iterate over all multi input indices
                connectionsToReplace.Remove(connection);
            }

            // now create the entries for those that will be reconnected after the instance has been replaced. Take care of the multi input order
            //connectionsToReplace.Reverse();

            foreach (var con in connectionsToReplace)
            {
                Parent.ReplaceConnection(con);
            }
            
            return false;
        }

        private bool TryCreateNewInstance(Instance? parentInstance,
                                           [NotNullWhen(true)] out Instance? newInstance)
        {
            var path = parentInstance == null ? [Id] : parentInstance.InstancePath.Append(Id).ToArray();
            var parent = parentInstance?.SymbolChild;
            var pathHash = HashCodeOf(path);
            lock (_creationLock)
            {
                if (_instancesOfSelf.TryGetValue(pathHash, out newInstance))
                {
                    // instance already exists
                    //return true;
                    throw new InvalidOperationException($"Instance {Name} with id ({Id}) already exists at path {string.Join(" > ", path)}");
                }

                if (!TryCreateInstance(parent, path, out newInstance, out var reason))
                {
                    Log.Error(reason);
                    return false;
                }

                // cache property accesses for performance
                var newInstanceInputDefinitions = Symbol.InputDefinitions;
                var newInstanceInputDefinitionCount = newInstanceInputDefinitions.Count;

                var newInstanceInputs = newInstance.Inputs;
                var newInstanceInputCount = newInstanceInputs.Count;

                var symbolChildInputs = Inputs;

                // set up the inputs for the child instance
                for (int i = 0; i < newInstanceInputDefinitionCount; i++)
                {
                    if (i >= newInstanceInputCount)
                    {
                        Log.Warning($"Skipping undefined input index");
                        continue;
                    }

                    var inputDefinitionId = newInstanceInputDefinitions[i].Id;
                    var inputSlot = newInstanceInputs[i];
                    if (!symbolChildInputs.TryGetValue(inputDefinitionId, out var input))
                    {
                        Log.Warning($"Skipping undefined input: {inputDefinitionId}");
                        continue;
                    }

                    inputSlot.Input = input;
                    inputSlot.Id = inputDefinitionId;
                }

                // cache property accesses for performance
                var childOutputDefinitions = Symbol.OutputDefinitions;
                var childOutputDefinitionCount = childOutputDefinitions.Count;

                var childOutputs = newInstance.Outputs;

                var symbolChildOutputs = Outputs;

                // set up the outputs for the child instance
                for (int i = 0; i < childOutputDefinitionCount; i++)
                {
                    Debug.Assert(i < childOutputs.Count);
                    var outputDefinition = childOutputDefinitions[i];
                    var id = outputDefinition.Id;
                    if (i >= childOutputs.Count)
                    {
                        Log.Warning($"Skipping undefined output: {id}");
                        continue;
                    }

                    var outputSlot = childOutputs[i];
                    outputSlot.Id = id;
                    var symbolChildOutput = symbolChildOutputs[id];
                    if (outputDefinition.OutputDataType != null)
                    {
                        // output is using data, so link it
                        if (outputSlot is IOutputDataUser outputDataConsumer)
                        {
                            outputDataConsumer.SetOutputData(symbolChildOutput.OutputData);
                        }
                    }

                    outputSlot.DirtyFlag.Trigger = symbolChildOutput.DirtyFlagTrigger;
                    outputSlot.IsDisabled = symbolChildOutput.IsDisabled;
                }
            }

            return true;

            bool TryCreateInstance(Symbol.Child? parent, Guid[] newInstancePath,
                                   [NotNullWhen(true)] out Instance? newInstance,
                                   [NotNullWhen(false)] out string? reason2)
            {
                if(parent != null)
                {
                    if(parent.Symbol != Parent)
                    {
                        throw new InvalidOperationException($"Parent symbol {parent.Symbol} does not match {Symbol}");
                    }
                    
                    if (newInstancePath[^2] != parent.Id)
                    {
                        throw new InvalidOperationException($"Instance path does not match parent id {parent.Id}");
                    }
                    
                    // check recursion
              /*      var currentParent = parent;
                    while (currentParent != null)
                    {
                        if (currentParent.Symbol == Symbol)
                        {
                            throw new InvalidOperationException($"Recursion detected in {Symbol} with parent {currentParent.Symbol}");
                        }
                        
                        currentParent = currentParent.Parent;
                    }
                    */
                }
                else
                {
                    if(Parent != null)
                        throw new InvalidOperationException("symbol child has no parent but parent instance provided is not null");
                }

                // make sure we're not instantiating a child that needs to be updated again later
                //Symbol.UpdateInstanceType();

                if (!TryInstantiate(out newInstance, out reason2))
                {
                    Log.Error(reason2);
                    return false;
                }
                
                if (!_instancesOfSelf.TryAdd(pathHash, newInstance))
                {
                    throw new InvalidOperationException($"Attempted to create a new instance when one already exists at that path");
                }
                
                newInstance.InitializeSymbolChildInfo(this, parent, newInstancePath, pathHash);

                Instance.SortInputSlotsByDefinitionOrder(newInstance);
                
                var childPath = new Guid[newInstancePath.Length + 1];
                Array.Copy(newInstancePath, childPath, newInstancePath.Length);

                // populates child instances of the new instance
                foreach (var child in Symbol.Children.Values)
                {
                    childPath[^1] = child.Id;
                    if (child.TryGetOrCreateInstance(childPath, out var childInstance, out var created))
                    {
                        if (created)
                        {
                            if (!newInstance.Children.TryGetChildInstance(child.Id, out var inst2, false))
                                throw new InvalidOperationException($"Child instance {child.Id} not found in {newInstance}");
                        
                            if(inst2 != childInstance)
                            {
                                throw new InvalidOperationException($"Child instance {child.Id} does not match {childInstance}");
                            }
                        }
                        else
                        {
                            // this operator has likely recompiled and the child instance is orphaned from the previous version of this op 
                            if (childInstance.Parent != newInstance)
                            {
                                throw new InvalidOperationException($"Child instance {childInstance} has a different parent than expected");
                            }
                            
                            /* // moved to the instance Dispose method
                            for (int i = 0; i < childInstance.Inputs.Count; i++)
                            {
                                var input = childInstance.Inputs[i];
                                while (input.HasInputConnections)
                                    input.RemoveConnection();
                            }
                            */
                            
                            if(childInstance.SymbolChild.Symbol is {NeedsReconnections: true , IsReconnecting: false})
                            {
                                // if the symbol needs reconnections, we need to reconnect
                                childInstance.SymbolChild.Symbol.ReconnectAll();
                            }
                            
                            if (!newInstance.Children.TryGetChildInstance(child.Id, out var inst2, false))
                            {
                                throw new InvalidOperationException($"Child instance {child.Id} does not match {childInstance}");
                            }

                            if (inst2 != childInstance)
                            {
                                throw new InvalidOperationException($"Child instance found does not match {childInstance}");
                            }
                            
                            childInstance.MarkResourceDirectoriesDirty();
                        }
                    }
                }

                CreateConnectionsForInstance(newInstance);

                return true;

                bool TryInstantiate([NotNullWhen(true)] out Instance? instance,
                                    [NotNullWhen(false)] out string? reason3)
                {
                    var symbolPackage = Symbol.SymbolPackage;
                    if (symbolPackage.AssemblyInformation.OperatorTypeInfo.TryGetValue(Symbol.Id, out var typeInfo))
                    {
                        var constructor = typeInfo.GetConstructor();
                        try
                        {
                            instance = (Instance)constructor.Invoke();
                            reason3 = string.Empty;
                            return true;
                        }
                        catch (Exception e)
                        {
                            reason3 = $"Failed to create instance of type {Symbol.InstanceType} with id {Id}: {e}";
                            instance = null;
                            return false;
                        }
                    }

                    Log.Error($"No constructor found for {Symbol.InstanceType}. This should never happen!! Please report this");

                    try
                    {
                        // create instance through reflection
                        instance = Activator.CreateInstance(Symbol.InstanceType,
                                                            AssemblyInformation.ConstructorBindingFlags,
                                                            binder: null,
                                                            args: Array.Empty<object>(),
                                                            culture: null) as Instance;

                        if (instance is null)
                        {
                            reason3 = $"(Instance creation fallback failure) Failed to create instance of type " +
                                      $"{Symbol.InstanceType} with id {Id} - result was null";
                            return false;
                        }

                        Log.Warning($"(Instance creation fallback) Created instance of type {Symbol.InstanceType} with id {Id} through reflection");

                        reason3 = string.Empty;
                        return true;
                    }
                    catch (Exception e)
                    {
                        reason3 = $"(Instance creation fallback failure) Failed to create instance of type {Symbol.InstanceType} with id {Id}: {e}";
                        instance = null;
                        return false;
                    }
                }
            }
        }

        private void CreateConnectionsForInstance(Instance newInstance)
        {
            // create connections between child instances populated with CreateAndAddNewChildInstance
            var connections = Symbol.Connections;
            newInstance.NeedsInternalReconnections = false;

            // if connections already exist for the symbol, remove any that shouldn't exist anymore
            if (connections.Count != 0)
            {
                var conHashToCount = new Dictionary<ulong, int>(connections.Count);
                for (var index = 0; index < connections.Count; index++) // warning: the order in which these are processed matters
                {
                    var connection = connections[index];
                    ulong highPart = 0xFFFFFFFF & (ulong)connection.TargetSlotId.GetHashCode();
                    ulong lowPart = 0xFFFFFFFF & (ulong)connection.TargetParentOrChildId.GetHashCode();
                    ulong hash = (highPart << 32) | lowPart;
                    conHashToCount.TryGetValue(hash, out int count);

                    if (!newInstance.TryAddConnection(connection, count, true))
                    {
                        Log.Warning($"Removing obsolete connecting in {Symbol}...");
                        connections.RemoveAt(index);
                        index--;
                        continue;
                    }

                    conHashToCount[hash] = count + 1;
                }
            }

            // connect animations if available
            Symbol.Animator.CreateUpdateActionsForExistingCurves(newInstance.Children.Values);

            if (_isBypassed)
            {
                SetBypassForInstance(newInstance, true, invalidate: false);
            }
            
            if(newInstance.Symbol is { NeedsReconnections: true, IsReconnecting: false })
            {
                // if the symbol needs reconnections, we need to reconnect all connections
                newInstance.Symbol.ReconnectAll();
            }
        }

        internal void AddConnectionToInstances(Connection connection, int multiInputIndex, bool allowCreate)
        {
            lock (_creationLock)
            {
                foreach (var instance in _instancesOfSelf.Values)
                {
                    instance.TryAddConnection(connection, multiInputIndex, allowCreate);
                }
            }
        }

        internal void RemoveConnectionFromInstances(Connection connection, int multiInputIndex)
        {
            lock (_creationLock)
            {
                foreach (var instance in _instancesOfSelf.Values)
                {
                    if (instance.TryGetTargetSlot(connection, out var targetSlot, false))
                    {
                        targetSlot.RemoveConnection(multiInputIndex);
                    }
                }
            }
        }

        internal void InvalidateInputDefaultInInstances(in Guid inputId)
        {
            lock (_creationLock)
            {
                foreach (var instance in _instancesOfSelf.Values)
                {
                    var inputSlots = instance.Inputs;
                    for (int i = 0; i < inputSlots.Count; i++)
                    {
                        var slot = inputSlots[i];
                        if (slot.Id != inputId)
                            continue;

                        if (!slot.Input.IsDefault)
                            continue;

                        slot.DirtyFlag.Invalidate();
                        break;
                    }
                }
            }
        }

        internal void InvalidateInputInChildren(in Guid inputId, in Guid childId)
        {
            lock (_creationLock)
            {
                foreach (var instanceInfo in _instancesOfSelf)
                {
                    var instance = instanceInfo.Value;
                    
                    //var child = instance.Children[childId];
                    if (!instance.Children.TryGetChildInstance(childId, out var child))
                    {
                        Log.Debug("Failed to invalidate missing child");
                        continue;
                    }

                    var inputSlots = child.Inputs;
                    for (int j = 0; j < inputSlots.Count; j++)
                    {
                        var slot = inputSlots[j];
                        if (slot.Id != inputId)
                            continue;

                        slot.DirtyFlag.Invalidate();
                        break;
                    }
                }
            }
        }

        internal void SortInputSlotsByDefinitionOrder()
        {
            lock (_creationLock)
            {
                foreach (var instance in _instancesOfSelf.Values)
                {
                    Instance.SortInputSlotsByDefinitionOrder(instance);
                }
            }
        }

        internal void RemoveDisposedInstance(Instance child, int? hash)
        {
            hash ??= HashCodeOf(child.InstancePath);

            lock (_creationLock)
            {
                if (!_instancesOfSelf.Remove(hash.Value))
                {
                    Log.Error($"Could not find instance {child} to remove from {this}");
                }
            }
        }

        internal void PrepareForReload()
        {
            DestroyAndClearAllInstances(Symbol.SymbolPackage);
        }

        public bool TryGetOrCreateInstance(IReadOnlyList<Guid> path, [NotNullWhen(true)] out Instance? instance, out bool created, bool allowCreate = true)
        {
            // throw exceptions if the path is invalid
            if (path.Count == 0)
            {
                throw new ArgumentException("Path must not be empty");
            }
            
            if(!path[^1].Equals(Id))
            {
                throw new ArgumentException($"Path must end with {Id}");
            }

            if (Parent == null)
            {
                if(path.Count != 1)
                    throw new ArgumentException("Path must be of length 1 if parent is null");
                
                if(path[0] != Id)
                    throw new ArgumentException($"Path must be {Id} if parent is null");
            }
            else if (path.Count < 2)
            {
                throw new ArgumentException("Path must be of length 2 or more if parent is not null");
            }
            
            var hash = HashCodeOf(path);

            lock (_creationLock)
            {
                if (_instancesOfSelf.TryGetValue(hash, out instance))
                {
                    // instance already exists
                    created = false;
                    EnsureInstanceSlotsAreConnected(path, instance);
                    return true;
                }
                
                if(!allowCreate)
                {
                    created = false;
                    return false;
                }

                if (Parent == null)
                {
                    created = TryCreateNewInstance(null, out instance);
                    return created;
                }

                if (TryGetParentInstance(path, out created, out var parentInstance))
                {
                    // try to get our instance straight from the parent
                    if (!parentInstance.Children.TryGetChildInstance(Id, out instance, false))
                    {
                        // since we dont exist yet, lets create us
                        created = TryCreateNewInstance(parentInstance, out instance);
                        return created;
                    }

                    // if we're here, we already exist in the parent instance
                    // we just need to make sure we add our instance to our own collection
                    Debug.Assert(_instancesOfSelf.ContainsKey(hash));
                    return true;
                }
            }

            created = false;
            return false;

            Child GetParentAsChild(IReadOnlyList<Guid> readOnlyList)
            {
                var parentSymbolChildId = readOnlyList[^2];
                var parentSymbolChild = Parent.ChildrenCreatedFromMe[parentSymbolChildId];
                return parentSymbolChild;
            }

            bool TryGetParentInstance(IReadOnlyList<Guid> guids, out bool wasCreated, [NotNullWhen(true)] out Instance? parentInstance)
            {
                var parentSymbolChild = GetParentAsChild(guids);
                var parentPath = guids.SkipLast(1).ToArray();
                var gotParent = parentSymbolChild.TryGetOrCreateInstance(parentPath, out parentInstance, out wasCreated);
                return gotParent;
            }

            void EnsureInstanceSlotsAreConnected(IReadOnlyList<Guid> instancePath, Instance existingInstance)
            {
                if (Symbol.NeedsReconnections)
                {
                    Symbol.ReconnectAll();
                }

                if (existingInstance.NeedsInternalReconnections)
                {
                    ReconnectChildrenOf(existingInstance);
                }

                if (!existingInstance.NeedsReconnectionToPeers) 
                {
                    return;
                }

                if (Parent is null)
                {
                    // if we have no parent we have no connections to reconnect
                    Log.Warning("Instance has no parent but needs reconnections - this probably shouldn't happen", existingInstance);
                    return;
                }

                if (Parent.NeedsReconnections || Parent.IsReconnecting)
                {
                    // if the parent needs reconnections or is currently in the process of reconnecting, we don't need to do anything
                    return;
                }
                
                if(TryGetParentInstance(instancePath, out var wasCreated, out var parent))
                {
                    if (wasCreated)
                    {
                        // if the parent was just created via this invocation, then this instance should have been marked as not needing reconnections
                        // so we can skip this
                        Log.Debug("Recreated parent instance from child reference", parent);
                        Debug.Assert(existingInstance.NeedsReconnectionToPeers == false, "Instance's parent was created but still needs reconnections");
                        return;
                    }
                    
                    if (parent.SymbolChild._isReconnecting)
                    {
                        // no reconnection necessary as we are currently reconnecting
                        return;
                    }
                    
                    if (parent.NeedsInternalReconnections)
                    {
                        parent.SymbolChild.ReconnectChildrenOf(parent);
                        Log.Debug("Regenerated connections for parent", parent);
                        Debug.Assert(existingInstance.NeedsReconnectionToPeers == false, "Instance should have had its connections regenerated, but still needs reconnections");
                    }
                }
                else 
                {
                    Log.Error("Instance needs reconnections but parent is null - this probably shouldn't happen", existingInstance);
                }
            }
        }
        

        private static int HashCodeOf(IReadOnlyList<Guid> path)
        {
            int hash = path[0].GetHashCode();
            for (int i = 1; i < path.Count; i++)
            {
                hash = HashCode.Combine(hash, path[i].GetHashCode());
            }
            return hash;
        }

        public void ClearPreviousId()
        {
            PreviousId = null;
        }

  

        public bool SearchForChild(Guid search, [NotNullWhen(true)] out Child? child, [NotNullWhen(true)] out IReadOnlyList<Guid>? path)
        {
            return SearchForChild(search, ReadOnlySpan<Guid>.Empty, out child, out path);
        }

        private bool SearchForChild(Guid search, ReadOnlySpan<Guid> path, out Child? child, [NotNullWhen(true)] out IReadOnlyList<Guid>? fullPath)
        {
            Span<Guid> pathIncludingMe = stackalloc Guid[path.Length + 1];
            path.CopyTo(pathIncludingMe);
            pathIncludingMe[^1] = Id;
            if (Id == search)
            {
                child = this;
                fullPath = pathIncludingMe.ToArray();
                return true;
            }

            var symbol = Symbol;
            foreach (var symbolChild in symbol.Children.Values)
            {
                if(symbolChild.SearchForChild(search, pathIncludingMe, out var foundChild, out var foundPath))
                {
                    child = foundChild;
                    fullPath = foundPath;
                    return true;
                }
            }
            
            child = null;
            fullPath = null;
            return false;
        }

        private bool _isReconnecting;
        internal void ReconnectAllChildren()
        {
            _isReconnecting = true;
            lock (_creationLock)
            {
                foreach (var inst in _instancesOfSelf.Values)
                {
                    ReconnectChildrenOf(inst);
                }
            }
            _isReconnecting = false;
        }

        private void ReconnectChildrenOf(Instance inst)
        {
            inst.NeedsInternalReconnections = false;
            
            var children = inst.Children;
            // first removes all connections from children
            foreach (var child in Symbol.Children.Values)
            {
                if (children.TryGetChildInstance(child.Id, out var childInst, false))
                {
                    childInst.DisconnectInputs();
                }
            }
                    
            // then reconnects them from scratch
            CreateConnectionsForInstance(inst);
        }
    }
}