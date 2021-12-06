namespace LIB.OPC.Client.Subscribe
{
    public class SubscriptionData
    {
        public uint SubscriptionId { get; set; }

        public uint ClientHandle { get; set; }

        public object DataValue { get; set; }
    }
}
