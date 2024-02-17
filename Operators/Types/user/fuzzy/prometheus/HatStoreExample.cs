using T3.Core.Operator;
using T3.Core.Operator.Attributes;
using T3.Core.Operator.Slots;

namespace T3.Operators.Types.Id_1bcfd505_5078_4ec5_878b_dcdeec15b173
{
    public class HatStoreExample : Instance<HatStoreExample>
    {

        [Output(Guid = "6f0809d3-0daa-4fd3-8e9a-1c4d6dda9f8a")]
        public readonly Slot<System.Collections.Generic.List<float>> values = new Slot<System.Collections.Generic.List<float>>();

        [Input(Guid = "1e31c010-ac25-4354-8763-0844d6cf43f1")]
        public readonly InputSlot<int> Size = new InputSlot<int>();

        [Input(Guid = "bd26eb79-9ac3-4df7-8a57-8d7f0f14818d")]
        public readonly InputSlot<int> Backlog = new InputSlot<int>();

    }
}

