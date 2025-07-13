using System;
using T3.Core.Operator;
using T3.Core.Operator.Attributes;
using T3.Core.Operator.Slots;
using System.Runtime.InteropServices;

namespace Lib.flow.testing{
    [Guid("b4a9f19a-bbb0-4d65-8d4a-560460e2505c")]
    internal sealed class AllTests : Instance<AllTests>
    {
        [Output(Guid = "f9ddc7da-91fa-4a7c-b59d-d1ba8fc5eb74")]
        public readonly Slot<string> Result = new Slot<string>();

        [Input(Guid = "e8852264-b6e0-4701-b286-c074ea87175f")]
        public readonly InputSlot<bool> Trigger = new InputSlot<bool>();

        [Input(Guid = "9a0cfe2c-30d2-48b8-8013-039ea3e82172")]
        public readonly InputSlot<bool> TriggerRebuild = new InputSlot<bool>();


    }
}

