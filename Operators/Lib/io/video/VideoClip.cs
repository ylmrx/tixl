using T3.Core.DataTypes;
using T3.Core.Operator;
using T3.Core.Operator.Attributes;
using T3.Core.Operator.Slots;
using System.Runtime.InteropServices;

namespace Lib.io.video{
    [Guid("b4288f74-7997-40f4-bc4c-2833bf3797de")]
    internal sealed class VideoClip : Instance<VideoClip>
    {

        [Output(Guid = "1503d637-b9fe-4323-8626-e54252e997ee")]
        public readonly TimeClipSlot<T3.Core.DataTypes.Command> Output2 = new TimeClipSlot<T3.Core.DataTypes.Command>();


    }
}

