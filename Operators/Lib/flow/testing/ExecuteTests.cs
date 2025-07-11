using T3.Core.Animation;
using T3.Core.Utils;
using T3.Core.Video;
// ReSharper disable ForCanBeConvertedToForeach

namespace Lib.flow.testing;

[Guid("83cb923e-a387-4be2-b391-4111c7bd90fe")]
internal sealed class ExecuteTests : Instance<ExecuteTests>
{
    [Output(Guid = "0b0628f8-94c6-450e-83ba-515da78c7229")]
    public readonly Slot<Command> Command = new();
    
    [Output(Guid = "229A2DD4-419F-43B9-AECD-12EAB9B25DEF")]
    public readonly Slot<string> Result = new();    
    

    public ExecuteTests()
    {
        Result.UpdateAction = Update;
        Command.UpdateAction = Update;
    }
    
    private void Update(EvaluationContext context)
    {
        ScreenshotWriter.Update();
        
        var testframeId = "_TestFrame";
        var testResultId = "_TestResult";
        var testActionId = "_TestAction";
        
        var testIndex = Playback.FrameCount;
        var isTestRoot = !context.IntVariables.TryGetValue(testframeId, out var frame);
        
        if (isTestRoot)
        {
            context.IntVariables[testframeId] = testIndex;
            _testResult.Clear();
        }
        else
        {
            Log.Debug("Forwarding test...", this);
        }
        
        var needsUpdate = MathUtils.WasChanged(TriggerTest.GetValue(context), ref _testTriggered);
        needsUpdate |= MathUtils.WasChanged(TriggerUpdateReferences.GetValue(context), ref _updateReferences);

        if (isTestRoot) {
            if (!needsUpdate)
            {
                return;
            }
        }

        TriggerTest.SetTypedInputValue(false);
        TriggerUpdateReferences.SetTypedInputValue(false);

        if (isTestRoot)
        {
            if (_updateReferences)
            {
                context.ObjectVariables[testActionId] = "UpdateReferences";
                Log.Debug("Update references ..." +  testIndex, this);
            }
            else if (_testTriggered)
            {
                context.ObjectVariables[testActionId] = "Test";
                Log.Debug("Started test ..." +  testIndex, this);
            }
            else
            {
                return;
            }
            context.ObjectVariables[testResultId] = _testResult;
        }

        
        //
        var commands = Commands.CollectedInputs;
        
        // do preparation if needed
        for (int i = 0; i < commands.Count; i++)
        {
            commands[i].Value?.PrepareAction?.Invoke(context);
        }

        // execute commands
        for (int i = 0; i < commands.Count; i++)
        {
            Log.Debug("  " + i,this);
            commands[i].GetValue(context);
        }

        // cleanup after usage
        for (int i = 0; i < commands.Count; i++)
        {
            commands[i].Value?.RestoreAction?.Invoke(context);
        }

        Command.DirtyFlag.Clear();
        
        if (isTestRoot)
        {
            if (!context.IntVariables.Remove(testframeId))
            {
                Log.Warning($"Expected {testframeId} variable for roottest");
            }
            
            Result.Value= _testResult.ToString();
        }
        else
        {
            Result.Value= "intermediate test";
        }
    }

    private static StringBuilder _testResult = new();
    private bool _testTriggered;
    private bool _updateReferences;
    
    [Input(Guid = "C6CAA262-DF3C-44CB-8DBE-D9D733A3C63A")]
    public readonly MultiInputSlot<Command> Commands = new();

    [Input(Guid = "31937a13-1e53-45dd-8e6f-91ca5f3aaa19")]
    public readonly InputSlot<bool> TriggerTest = new();

    [Input(Guid = "E1989C94-8F51-414A-9CA0-33631875A9DF")]
    public readonly InputSlot<bool> TriggerUpdateReferences = new();
    
}