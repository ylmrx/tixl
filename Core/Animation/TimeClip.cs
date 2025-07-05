using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using T3.Core.Logging;
using T3.Core.Operator.Slots;
using T3.Serialization;

namespace T3.Core.Animation;

public class TimeClip : IOutputData
{
    public TimeClip()
    {
        var t = Playback.Current != null
                    ? (float)Playback.Current.TimeInBars
                    : 0;
        _timeRange = new TimeRange(t, t + DefaultClipDuration);
        _sourceRange = new TimeRange(t, t + DefaultClipDuration);
    }

    private const float DefaultClipDuration = 4f;
    public Guid Id { get; set; }

    private TimeRange _timeRange;
    public ref TimeRange TimeRange => ref _timeRange;

    private TimeRange _sourceRange;
    public ref TimeRange SourceRange => ref _sourceRange;

    public int LayerIndex { get; set; } = 0;

    public Type DataType => typeof(TimeClip);

    public bool IsClipOverlappingOthers(IEnumerable<TimeClip> allTimeClips)
    {
        foreach (var otherClip in allTimeClips)
        {
            if (otherClip == this)
                continue;

            if (LayerIndex != otherClip.LayerIndex)
                continue;

            var start = TimeRange.Start;
            var end = TimeRange.End;
            var otherStart = otherClip.TimeRange.Start;
            var otherEnd = otherClip.TimeRange.End;

            if (otherEnd <= start || otherStart >= end)
                continue;

            return true;
        }

        return false;
    }

    #region serialization
    public void ToJson(JsonTextWriter writer)
    {
        writer.WritePropertyName("TimeClip");
        writer.WriteStartObject();
        writer.WritePropertyName("TimeRange");
        writer.WriteStartObject();
        writer.WriteValue("Start", _timeRange.Start);
        writer.WriteValue("End", _timeRange.End);
        writer.WriteEndObject();
        writer.WritePropertyName("SourceRange");
        writer.WriteStartObject();
        writer.WriteValue("Start", _sourceRange.Start);
        writer.WriteValue("End", _sourceRange.End);
        writer.WriteEndObject();
        writer.WriteValue("LayerIndex", LayerIndex);
        writer.WriteEndObject();
    }

    public void ReadFromJson(JToken json)
    {
        var timeClip = json["TimeClip"];
        if (timeClip != null)
        {
            var timeRange = timeClip["TimeRange"];
            if (timeRange != null)
            {
                _timeRange = new TimeRange(timeRange["Start"].Value<float>(), timeRange["End"].Value<float>());
            }

            var sourceRange = timeClip["SourceRange"];
            if (sourceRange != null)
            {
                _sourceRange = new TimeRange(sourceRange["Start"].Value<float>(), sourceRange["End"].Value<float>());
            }

            LayerIndex = timeClip["LayerIndex"]?.Value<int>() ?? 0;
        }
    }
    #endregion

    public bool Assign(IOutputData outputData)
    {
        if (outputData is TimeClip otherTimeClip)
        {
            _timeRange = otherTimeClip.TimeRange;
            _sourceRange = otherTimeClip.SourceRange;
            LayerIndex = otherTimeClip.LayerIndex;

            return true;
        }

        Log.Error($"Trying to assign output data of type '{outputData.GetType()}' to 'TimeClip'.");

        return false;
    }

    public TimeClip Clone()
    {
        return new TimeClip()
                   {
                       Id = Guid.NewGuid(),
                       TimeRange = _timeRange,
                       SourceRange = _sourceRange,
                       LayerIndex = LayerIndex
                   };
    }
}