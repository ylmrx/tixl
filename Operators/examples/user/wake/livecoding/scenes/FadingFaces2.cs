namespace Examples.user.wake.livecoding.scenes;

[Guid("abbb6ec1-f8ea-474d-a492-6857af84dd71")]
 internal sealed class FadingFaces2 : Instance<FadingFaces2>
{

    [Output(Guid = "0798a196-3c9b-4912-9870-c462b415d7f8")]
    public readonly Slot<T3.Core.DataTypes.Command> Output2 = new Slot<T3.Core.DataTypes.Command>();

        [Input(Guid = "c69707be-1948-43a6-846d-b9c36bd387bf")]
        public readonly InputSlot<bool> ArpsTrigger = new InputSlot<bool>();


}