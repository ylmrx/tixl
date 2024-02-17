using T3.Core.Operator;
using T3.Core.Operator.Attributes;
using T3.Core.Operator.Slots;

namespace T3.Operators.Types.Id_47cf74a7_033a_4442_b4bd_107e18c1f360
{
    public class FountainHatExample : Instance<FountainHatExample>
    {

        [Output(Guid = "8bc41920-a8b6-4d12-a24a-76066b389dda")]
        public readonly Slot<SharpDX.Direct3D11.Texture2D> Output = new Slot<SharpDX.Direct3D11.Texture2D>();

    }
}

