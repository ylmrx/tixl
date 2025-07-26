using System;
using T3.Core.Operator;
using T3.Core.Operator.Attributes;
using T3.Core.Operator.Slots;
using System.Runtime.InteropServices;

namespace Lib.flow.testing{
    [Guid("202467cd-e898-4869-b08c-804ca5db5b45")]
    internal sealed class IoTests : Instance<IoTests>
    {
        [Output(Guid = "7ccb7b18-f64a-4938-ab09-9b79b2962af5")]
        public readonly Slot<string> Result = new Slot<string>();


    }
}

