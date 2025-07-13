using System.Diagnostics;
using T3.Core.Animation;
using T3.Core.Utils;
using T3.Core.Video;

// ReSharper disable ForCanBeConvertedToForeach

namespace Lib.flow.testing;

[Guid("83cb923e-a387-4be2-b391-4111c7bd90fe")]
internal sealed class ExecuteTests : Instance<ExecuteTests>
{
    // [Output(Guid = "0b0628f8-94c6-450e-83ba-515da78c7229")]
    // public readonly Slot<Command> Command = new();
    //
    [Output(Guid = "229A2DD4-419F-43B9-AECD-12EAB9B25DEF")]
    public readonly Slot<string> Result = new();

    public ExecuteTests()
    {
        Result.UpdateAction = Update;
        // Command.UpdateAction = Update;
    }

    private void Update(EvaluationContext context)
    {
        var testframeId = "_TestFrame";
        var testResultId = "_TestResult";
        var testActionId = "_TestAction";

        var onlyShowFails = OnlyShowFails.GetValue(context);

        var testIndex = Playback.FrameCount;
        var isTestRoot = !context.IntVariables.TryGetValue(testframeId, out var frame);

        if (isTestRoot)
        {
            context.IntVariables[testframeId] = testIndex;
            _testResults.Clear();
        }
        else
        {
            Log.Debug("Forwarding test...", this);
        }

        var needsUpdate = MathUtils.WasChanged(TriggerTest.GetValue(context), ref _testTriggered);
        needsUpdate |= MathUtils.WasChanged(TriggerUpdateReferences.GetValue(context), ref _updateReferences);

        if (isTestRoot && !needsUpdate)
            return;

        TriggerTest.SetTypedInputValue(false);
        TriggerUpdateReferences.SetTypedInputValue(false);

        if (isTestRoot)
        {
            if (_updateReferences)
            {
                _stopwatch.Restart();
                context.ObjectVariables[testActionId] = "UpdateReferences";
                Log.Debug("Update references ..." + testIndex, this);
            }
            else if (_testTriggered)
            {
                _stopwatch.Restart();
                context.ObjectVariables[testActionId] = "Test";
                Log.Debug("Started test ..." + testIndex, this);
            }
            else
            {
                return;
            }

            context.ObjectVariables[testResultId] = _testResults;
        }

        //
        var commandResults = SubTests.CollectedInputs;

        // // do preparation if needed
        // for (int i = 0; i < commandResults.Count; i++)
        // {
        //     commandResults[i].Value?.PrepareAction?.Invoke(context);
        // }

        // execute commands
        for (int i = 0; i < commandResults.Count; i++)
        {
            commandResults[i].DirtyFlag.ForceInvalidate();
            commandResults[i].GetValue(context);
        }

        // cleanup after usage
        // for (int i = 0; i < commandResults.Count; i++)
        // {
        //     commandResults[i].Value?.RestoreAction?.Invoke(context);
        // }
        
        if (isTestRoot)
        {
            if (!context.IntVariables.Remove(testframeId))
                Log.Warning($"Expected {testframeId} variable for roottest");
            
            _stopwatch.Stop();

            _stringBuilder.Clear();
            var countFails = 0;
            var countSuccess = 0;
            foreach (var line in _testResults)
            {
                if (line.Contains("FAILED"))
                {
                    _stringBuilder.AppendLine(line);
                    countFails++;
                }
                else if (line.Contains("PASSED"))
                {
                    countSuccess++;
                    if(!onlyShowFails)
                        _stringBuilder.AppendLine(line);
                }
            }

            var countTotal = countFails + countSuccess;
            var passedLabel = countFails == 0 ? "SUCCESS" : "FAILED";

            _stringBuilder.Insert(0,$"{passedLabel}:   {countSuccess} / {countTotal}  {_stopwatch.ElapsedMilliseconds * 0.001:0.0s}\n\n");
            
            Result.Value = _stringBuilder.ToString();
        }
        else
        {
            Result.Value = "intermediate test";
        }
        
        
    }

    private readonly StringBuilder _stringBuilder = new();
    private readonly List<string> _testResults = [];
    private bool _testTriggered;
    private bool _updateReferences;

    private readonly Stopwatch _stopwatch = new();

    [Input(Guid = "18023689-423A-4FB8-BC3C-9E74D0148C78")]
    public readonly MultiInputSlot<string> SubTests = new();

    [Input(Guid = "31937a13-1e53-45dd-8e6f-91ca5f3aaa19")]
    public readonly InputSlot<bool> TriggerTest = new();

    [Input(Guid = "E1989C94-8F51-414A-9CA0-33631875A9DF")]
    public readonly InputSlot<bool> TriggerUpdateReferences = new();
    
    [Input(Guid = "13D66A6D-75E1-4AB0-805D-A2234B3334A4")]
    public readonly InputSlot<bool> OnlyShowFails = new();

    
}