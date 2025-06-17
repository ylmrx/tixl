using SharpDX.Direct3D11;
using Utilities = T3.Core.Utils.Utilities;

// Added for List<float>
// ReSharper disable MemberCanBePrivate.Global

namespace Lib.io.artnet;

[Guid("c9d7cd19-7fc6-4491-8dfa-3808725c7857")]
public sealed class PointsToMovingHeadList : Instance<PointsToMovingHeadList>
{
    [Output(Guid = "2a1298f8-073b-4a6f-b53e-083ef97fe931")]
    public readonly Slot<List<float>> Result = new(new List<float>(20));

    public PointsToMovingHeadList()
    {
        Result.UpdateAction = Update;
    }

    private void Update(EvaluationContext context)
    {
        var updateContinuously = true; // Consider making this an input
        try
        {
            var pointBuffer = Points.GetValue(context);

            if (pointBuffer == null)
            {
                Log.Warning("Point buffer is null.", this);
                return;
            }

            var d3DDevice = ResourceManager.Device;
            var immediateContext = d3DDevice.ImmediateContext;
            var fixtureChannelSize = FixtureChannelSize.GetValue(context);

            // Re-evaluate buffer setup conditions
            var needsBufferSetup = 
                                   _bufferWithViewsCpuAccess == null ||
                                   _bufferWithViewsCpuAccess.Buffer == null ||
                                   _bufferWithViewsCpuAccess.Buffer.Description.SizeInBytes != pointBuffer.Buffer.Description.SizeInBytes ||
                                   _bufferWithViewsCpuAccess.Buffer.Description.StructureByteStride != pointBuffer.Buffer.Description.StructureByteStride;

            if (needsBufferSetup)
            {
                try
                {
                    // Dispose existing buffer if it exists and needs replacement
                    if (_bufferWithViewsCpuAccess != null && _bufferWithViewsCpuAccess.Buffer != null)
                    {
                        if (_bufferWithViewsCpuAccess.Buffer.Description.SizeInBytes != pointBuffer.Buffer.Description.SizeInBytes)
                        {
                             Utilities.Dispose(ref _bufferWithViewsCpuAccess.Buffer);
                             _bufferWithViewsCpuAccess.Buffer = null; // Ensure it's nullified after dispose for proper re-creation
                        }
                    }
                    
                    _bufferWithViewsCpuAccess ??= new BufferWithViews();

                    if (_bufferWithViewsCpuAccess.Buffer == null ||
                        _bufferWithViewsCpuAccess.Buffer.Description.SizeInBytes != pointBuffer.Buffer.Description.SizeInBytes)
                    {
                        var bufferDesc = new BufferDescription
                        {
                            Usage = ResourceUsage.Default, // For CopyResource
                            BindFlags = BindFlags.UnorderedAccess | BindFlags.ShaderResource, // For SRV/UAV
                            SizeInBytes = pointBuffer.Buffer.Description.SizeInBytes,
                            OptionFlags = ResourceOptionFlags.BufferStructured,
                            StructureByteStride = pointBuffer.Buffer.Description.StructureByteStride,
                            CpuAccessFlags = CpuAccessFlags.Read // For MapSubresource
                        };
                        _bufferWithViewsCpuAccess.Buffer = new Buffer(ResourceManager.Device, bufferDesc);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Failed to setup structured buffer: " + e.Message, this);
                    return;
                }

                ResourceManager.CreateStructuredBufferSrv(_bufferWithViewsCpuAccess.Buffer, ref _bufferWithViewsCpuAccess.Srv);
                immediateContext.CopyResource(pointBuffer.Buffer, _bufferWithViewsCpuAccess.Buffer);
            }

            // Only map if the buffer was successfully created/updated
            if (_bufferWithViewsCpuAccess?.Buffer == null)
            {
                Log.Warning("Structured buffer is not available for mapping.", this);
                return;
            }

            _ = immediateContext.MapSubresource(_bufferWithViewsCpuAccess.Buffer, 0, MapMode.Read, MapFlags.None, out var sourceStream);

            using (sourceStream)
            {
                var elementCount = _bufferWithViewsCpuAccess.Buffer.Description.SizeInBytes /
                                   _bufferWithViewsCpuAccess.Buffer.Description.StructureByteStride;

                var points = sourceStream.ReadRange<Point>(elementCount);

                var resultItems = new List<float>(fixtureChannelSize * points.Length);

                for (var connectionIndex = 0; connectionIndex < points.Length; connectionIndex++)
                {
                    var point = points[connectionIndex];

                    // Create a new items list for each point
                    var items = new List<float>(fixtureChannelSize);
                    for (var i = 0; i < fixtureChannelSize; i++)
                    {
                        items.Add(0);
                    }

                    if (GetRotation.GetValue(context))
                        ProcessRotation(context, point, items); // Pass context directly, no need for evaluationContext arg twice

                    if (GetColor.GetValue(context))
                        ProcessColor(context, point, items);

                    if (GetF1.GetValue(context))
                        ProcessF1(context, point, items);

                    if (GetF2.GetValue(context))
                        ProcessF2(context, point, items);
                    
                    // CustomVar1
                    if (SetCustomVar1.GetValue(context) && CustomVar1Channel.GetValue(context) > 0)
                    {
                        InsertOrSet(items, CustomVar1Channel.GetValue(context) - 1, CustomVar1.GetValue(context));
                    }

                    // CustomVar2
                    if (SetCustomVar2.GetValue(context) && CustomVar2Channel.GetValue(context) > 0)
                    {
                        InsertOrSet(items, CustomVar2Channel.GetValue(context) - 1, CustomVar2.GetValue(context));
                    }

                    // CustomVar3
                    if (SetCustomVar3.GetValue(context) && CustomVar3Channel.GetValue(context) > 0)
                    {
                        InsertOrSet(items, CustomVar3Channel.GetValue(context) - 1, CustomVar3.GetValue(context));
                    }

                    // Append this point's items to the result list
                    resultItems.AddRange(items);
                }

                Result.Value = resultItems;
            }
            // Ensure UnmapSubresource is called outside the using block to guarantee it's always called after the stream is closed.
            // This is actually safer if `sourceStream` might throw during construction.
            immediateContext.UnmapSubresource(_bufferWithViewsCpuAccess.Buffer, 0);


            Result.DirtyFlag.Trigger = updateContinuously ? DirtyFlagTrigger.Animated : DirtyFlagTrigger.None;
        }
        catch (Exception e)
        {
            Log.Error("Failed to fetch GPU resource or process data: " + e.Message);
            // Optionally, you might want to clear Result.Value in case of an error
            Result.Value = new List<float>(); 
        }
    }

    private Vector2 _lastPanTilt = new(float.NaN, float.NaN);

    
private void ProcessRotation(EvaluationContext context, Point point, List<float> items)
{
    var rotation = point.Orientation;

    if (!float.IsNaN(rotation.X) && !float.IsNaN(rotation.Y) && !float.IsNaN(rotation.Z) && !float.IsNaN(rotation.W))
    {
        Vector3 initialForwardAxis;
        var axisOrder = AxisOrder.GetValue(context);

        switch (axisOrder)
        {
            case 0:
            case 1:
                initialForwardAxis = Vector3.UnitZ;
                break;
            default:
                Log.Warning($"Unsupported AxisOrder: {axisOrder}. Using default (Z-forward).", this);
                initialForwardAxis = Vector3.UnitZ;
                break;
        }

        var direction = Vector3.Transform(initialForwardAxis, rotation);

        float panValue, tiltValue;

        switch (axisOrder)
        {
            case 0:
                panValue = MathF.Atan2(direction.X, direction.Z);
                tiltValue = MathF.Atan2(direction.Y, MathF.Sqrt(direction.X * direction.X + direction.Z * direction.Z));
                break;
            case 1:
                panValue = MathF.Atan2(direction.X, direction.Y);
                tiltValue = MathF.Atan2(-direction.Z, MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y));
                Log.Warning("AxisOrder 0 (Standard XYZ) selected. Pan/Tilt calculations are for a typical 3D graphics setup (Z-forward, Y-up). Review if this matches your source data.", this);
                break;
            default:
                panValue = MathF.Atan2(direction.X, direction.Z);
                tiltValue = MathF.Atan2(direction.Y, MathF.Sqrt(direction.X * direction.X + direction.Z * direction.Z));
                break;
        }

        var panRange = PanRange.GetValue(context);
        var tiltRange = TiltRange.GetValue(context);

        if (panRange.X >= panRange.Y)
        {
            Log.Warning("PanRange.X must be less than PanRange.Y", this);
            return;
        }
        if (tiltRange.X >= tiltRange.Y)
        {
            Log.Warning("TiltRange.X must be less than TiltRange.Y", this);
            return;
        }

        var panMin = panRange.X * MathF.PI / 180f;
        var panMax = panRange.Y * MathF.PI / 180f;
        var tiltMin = tiltRange.X * MathF.PI / 180f;
        var tiltMax = tiltRange.Y * MathF.PI / 180f;

        // --- Improved Shortest Path Pan/Tilt Logic ---
        if (ShortestPathPanTilt.GetValue(context) && !float.IsNaN(_lastPanTilt.X) && !float.IsNaN(_lastPanTilt.Y))
        {
            var prevPan = _lastPanTilt.X;
            var prevTilt = _lastPanTilt.Y;

            // Unwrap panValue to be as close as possible to prevPan (multi-turn support)
            var panSpan = panMax - panMin;
            var unwrappedPan = panValue;
            if (panSpan > MathF.PI * 1.5f) // More than 270°, allow multi-turn
            {
                // Find the closest equivalent panValue to prevPan (add/subtract 2PI as needed)
                var turns = MathF.Round((prevPan - panValue) / (2 * MathF.PI));
                unwrappedPan = panValue + turns * 2 * MathF.PI;
                // Clamp to panMin/panMax
                if (unwrappedPan < panMin) unwrappedPan += 2 * MathF.PI;
                if (unwrappedPan > panMax) unwrappedPan -= 2 * MathF.PI;
            }

            var directPanDiff = MathF.Abs(unwrappedPan - prevPan);

            // Try the flip solution
            var flippedTilt = MathF.PI - tiltValue;
            var flippedPan = unwrappedPan + MathF.PI;
            // Wrap flippedPan into pan range
            while (flippedPan < panMin) flippedPan += 2 * MathF.PI;
            while (flippedPan > panMax) flippedPan -= 2 * MathF.PI;

            var flipPanDiff = MathF.Abs(flippedPan - prevPan);

            // Choose the solution with the smaller pan movement, but only if both are within panMin/panMax
            var directValid = (unwrappedPan >= panMin && unwrappedPan <= panMax);
            var flipValid = (flippedPan >= panMin && flippedPan <= panMax);

            if (flipValid && (!directValid || flipPanDiff < directPanDiff))
            {
                panValue = flippedPan;
                tiltValue = flippedTilt;
            }
            else
            {
                panValue = unwrappedPan;
                // tiltValue stays as is
            }
        }
        else
        {
            // For first frame or if shortest path not enabled, unwrap to closest
            if (!float.IsNaN(_lastPanTilt.X))
            {
                var panSpan = panMax - panMin;
                var turns = MathF.Round((_lastPanTilt.X - panValue) / (2 * MathF.PI));
                panValue = panValue + turns * 2 * MathF.PI;
                // Clamp to panMin/panMax
                if (panValue < panMin) panValue += 2 * MathF.PI;
                if (panValue > panMax) panValue -= 2 * MathF.PI;
            }
        }

        _lastPanTilt = new Vector2(panValue, tiltValue);

        if (InvertPan.GetValue(context))
        {
            panValue = panMax + panMin - panValue;
        }
        if (InvertTilt.GetValue(context))
        {
            tiltValue = tiltMax + tiltMin - tiltValue;
        }

        panValue = Math.Clamp(panValue, panMin, panMax);
        tiltValue = Math.Clamp(tiltValue, tiltMin, tiltMax);

        SetDmxCoarseFine(
            items,
            panValue,
            PanChannel.GetValue(context),
            PanFineChannel.GetValue(context),
            panMin,
            panMax,
            panMax - panMin
        );

        SetDmxCoarseFine(
            items,
            tiltValue,
            TiltChannel.GetValue(context),
            TiltFineChannel.GetValue(context),
            tiltMin,
            tiltMax,
            tiltMax - tiltMin
        );
    }
    else
    {
        Log.Warning("Invalid rotation quaternion", this);
    }
}


    private float UnwrapAngle(float angle, float prevAngle)
    {
        var diff = angle - prevAngle;
        if (diff > MathF.PI)
            return angle - 2 * MathF.PI;
        if (diff < -MathF.PI)
            return angle + 2 * MathF.PI;
        return angle;
    }

    // Helper for coarse/fine DMX logic
    private void SetDmxCoarseFine(List<float> items, float value, int coarseChannel, int fineChannel, float inMin, float inMax, float maxDegrees)
    {
        //Input Validation
        if (coarseChannel < 1 || (fineChannel > 0 && fineChannel < 1))
        {
            Log.Warning("Pan or Tilt channel number can't be less than 1", this);
            return;
        }

        var dmx16 = MapToDmx16(value, inMin, inMax, maxDegrees);

        if (fineChannel > 0)
        {
            // 16-bit mode
            var coarse = (dmx16 >> 8) & 0xFF;
            var fine = dmx16 & 0xFF;
            InsertOrSet(items, coarseChannel - 1, coarse);
            InsertOrSet(items, fineChannel - 1, fine);
        }
        else
        {
            // 8-bit mode
            var coarse = (int)Math.Round((dmx16 / 65535.0f) * 255.0f);
            InsertOrSet(items, coarseChannel - 1, coarse);
        }
    }

    // 16-bit DMX mapping
    private int MapToDmx16(float value, float inMin, float inMax, float maxDegrees)
    {
        var range = inMax - inMin;
        if (Math.Abs(range) < 0.0001f || float.IsNaN(range) || Math.Abs(maxDegrees) < 0.0001f || float.IsNaN(maxDegrees))
            return 0;
        var normalizedValue = (value - inMin) / range;
        var degreeValue = normalizedValue * maxDegrees;
        var safeMaxDegrees = Math.Abs(maxDegrees) < 0.0001f ? 0.0001f : maxDegrees;
        return (int)Math.Round((degreeValue / safeMaxDegrees) * 65535.0f);
    }

    private void ProcessColor(EvaluationContext context, Point point, List<float> items)
    {
        if (!float.IsNaN(point.Color.X) && !float.IsNaN(point.Color.Y) && !float.IsNaN(point.Color.Z))
        {
            var r = point.Color.X;
            var g = point.Color.Y;
            var b = point.Color.Z;

            if (RGBToCMY.GetValue(context))
            {
                // Convert RGB to CMY
                r = 1f - r;
                g = 1f - g;
                b = 1f - b;
            }

            var vR = Math.Clamp(r, 0f, 1f) * 255.0f;
            var vG = Math.Clamp(g, 0f, 1f) * 255.0f;
            var vB = Math.Clamp(b, 0f, 1f) * 255.0f;

            InsertOrSet(items, RedChannel.GetValue(context) - 1, (float)Math.Round(vR));
            InsertOrSet(items, GreenChannel.GetValue(context) - 1, (float)Math.Round(vG));
            InsertOrSet(items, BlueChannel.GetValue(context) - 1, (float)Math.Round(vB));

            if (AlphaChannel.GetValue(context) > 0)
            {
                InsertOrSet(items, AlphaChannel.GetValue(context) - 1, (float)Math.Round(point.Color.W * 255.0f));
            }
        }
        else
        {
            Log.Warning("Invalid color value", this);
        }
    }

    private void ProcessF1(EvaluationContext context, Point point, List<float> items)
    {
        if (!float.IsNaN(point.F1))
        {
            InsertOrSet(items, F1Channel.GetValue(context) - 1, (float)Math.Round(point.F1 * 255.0f));
        }
        else
        {
            Log.Warning("Invalid SomeFloatValue", this);
        }
    }

    private void ProcessF2(EvaluationContext context, Point point, List<float> items)
    {
        if (!float.IsNaN(point.F2))
        {
            InsertOrSet(items, F2Channel.GetValue(context) - 1, (float)Math.Round(point.F2 * 255.0f));
        }
        else
        {
            Log.Warning("Invalid AnotherFloatValue", this);
        }
    }

    private int MapToDmx(float value, float inMin, float inMax, float maxDegrees)
    {
        var range = inMax - inMin;
        if (Math.Abs(range) < 0.0001f || float.IsNaN(range))
        {
            Log.Warning($"MapToDmx: Avoided division by zero or NaN. inMin={inMin}, inMax={inMax}. Returning 0.", this);
            return 0;
        }

        if (Math.Abs(maxDegrees) < 0.0001f || float.IsNaN(maxDegrees))
        {
            Log.Warning($"MapToDmx: maxDegrees is zero or NaN. Avoiding division by zero. Returning 0.", this);
            return 0;
        }

        var normalizedValue = (value - inMin) / range;
        var degreeValue = normalizedValue * maxDegrees;
        var safeMaxDegrees = Math.Abs(maxDegrees) < 0.0001f ? 0.0001f : maxDegrees;
        return (int)Math.Round((degreeValue / safeMaxDegrees) * 255.0f);
    }

    private void InsertOrSet(List<float> list, int index, float value)
    {
        //Input Validation
        if (index < 0)
        {
            Log.Warning($"DMX Channel index {index + 1} can't be less than 0. Adjust FixtureChannelSize or Channel Assignments.", this);
            return;
        }

        if (index >= 0 && index < list.Count)
        {
            list[index] = value;
        }
        else if (index >= 0) // Only warn if index is positive and out of bounds
        {
            Log.Warning($"DMX Channel index {index + 1} is out of range (list size: {list.Count}).  Adjust FixtureChannelSize or Channel Assignments.", this);
        }
    }
    
    private BufferWithViews _bufferWithViewsCpuAccess = new();

        [Input(Guid = "61b48e46-c3d1-46e3-a470-810d55f30aa6")]
        public readonly InputSlot<T3.Core.DataTypes.BufferWithViews> Points = new();

        [Input(Guid = "1348ed7c-79f8-48c6-ac00-e60fb40050db")]
        public readonly InputSlot<int> FixtureChannelSize = new();

        [Input(Guid = "4922acd8-ab83-4394-8118-c555385c2ce9")]
        public readonly InputSlot<bool> GetRotation = new();

        [Input(Guid = "ba8d8f32-792c-4675-a5f5-415c16db8c66")]
        public readonly InputSlot<int> AxisOrder = new();

        [Input(Guid = "7bf3e057-b9eb-43d2-8e1a-64c1c3857ca1")]
        public readonly InputSlot<bool> InvertPan = new();

        [Input(Guid = "f85ecf9f-0c3d-4c10-8ba7-480aa2c7a667")]
        public readonly InputSlot<bool> InvertTilt = new();

        [Input(Guid = "e96655be-6bc7-4ca4-bf74-079a07570d74")]
        public readonly InputSlot<bool> ShortestPathPanTilt = new();

        [Input(Guid = "f50da250-606d-4a15-a25e-5458f540e527")]
        public readonly InputSlot<System.Numerics.Vector2> PanRange = new();

        [Input(Guid = "9000c279-73e4-4de8-a1f8-c3914eaaf533")]
        public readonly InputSlot<int> PanChannel = new();

        [Input(Guid = "4d4b3425-e6ad-4834-a8a7-06c9f9c2b909")]
        public readonly InputSlot<int> PanFineChannel = new();

        [Input(Guid = "6e8b4125-0e8c-430b-897d-2231bb4c8f6f")]
        public readonly InputSlot<System.Numerics.Vector2> TiltRange = new();

        [Input(Guid = "47d7294f-6f73-4e21-ac9a-0fc0817283fb")]
        public readonly InputSlot<int> TiltChannel = new();

        [Input(Guid = "4a40e022-d206-447c-bda3-d534f231c816")]
        public readonly InputSlot<int> TiltFineChannel = new();

        [Input(Guid = "5cdc69f7-45ec-4eec-bfb6-960d6245dafb")]
        public readonly InputSlot<bool> GetColor = new();

        [Input(Guid = "cf2c3308-8f3f-442d-a563-b419f12e7ad1")]
        public readonly InputSlot<bool> RGBToCMY = new();

        [Input(Guid = "013cc355-91d6-4ea6-b9f7-f1817b89e4a3")]
        public readonly InputSlot<int> RedChannel = new();

        [Input(Guid = "970769f4-116f-418d-87a7-cda28e44d063")]
        public readonly InputSlot<int> GreenChannel = new();

        [Input(Guid = "d755342b-9a9e-4c78-8376-81579d8c0909")]
        public readonly InputSlot<int> BlueChannel = new();

        [Input(Guid = "f13edebd-b44f-49e9-985e-7e3feb886fea")]
        public readonly InputSlot<int> AlphaChannel = new();

        [Input(Guid = "91c78090-be10-4203-827e-d2ef1b93317e")]
        public readonly InputSlot<bool> GetF1 = new();

        [Input(Guid = "b7061834-66aa-4f7f-91f9-10ebfe16713f")]
        public readonly InputSlot<int> F1Channel = new();

        [Input(Guid = "1cb93e97-0161-4a77-bbc7-ff30c1972cf8")]
        public readonly InputSlot<bool> GetF2 = new();

        [Input(Guid = "d77be0d1-5fb9-4d26-9e4a-e16497e4759c")]
        public readonly InputSlot<int> F2Channel = new();

        [Input(Guid = "25e5f0ce-5ec8-4c99-beb1-317c6911a128")]
        public readonly InputSlot<bool> SetCustomVar1 = new();

        [Input(Guid = "b08c920f-0d6b-4820-bc2d-81a47d5f1147")]
        public readonly InputSlot<int> CustomVar1Channel = new();

        [Input(Guid = "50e849e8-5582-432e-98f7-d8e036273864")]
        public readonly InputSlot<int> CustomVar1 = new();

        [Input(Guid = "18cc3a73-3a1a-4370-87b7-e5cd44f4a3ab")]
        public readonly InputSlot<bool> SetCustomVar2 = new();

        [Input(Guid = "098f1662-6f47-4dd0-9a73-4c4814aefb23")]
        public readonly InputSlot<int> CustomVar2Channel = new();

        [Input(Guid = "e7a48fe0-d788-4f12-a9d4-52472519da09")]
        public readonly InputSlot<int> CustomVar2 = new();

        [Input(Guid = "876ef5b5-f2c6-4501-9e55-00b9a553a2e3")]
        public readonly InputSlot<bool> SetCustomVar3 = new();

        [Input(Guid = "ac9a709e-6dc0-40ca-9f70-350e655a2630")]
        public readonly InputSlot<int> CustomVar3Channel = new();

        [Input(Guid = "d16d7c5c-2795-4fde-85fd-13b515191fbe")]
        public readonly InputSlot<int> CustomVar3 = new();
}