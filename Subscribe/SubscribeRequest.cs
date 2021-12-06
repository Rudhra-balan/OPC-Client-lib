namespace LIB.OPC.Client.Subscribe
{
    public class SubscribeRequest
    {
        public SubscribeRequest()
        {
            RequestedPublishingInterval = 10;

            RequestedMaxKeepAliveCount = 10;

            RequestedLifetimeCount = 30;

            PublishingEnabled = true;
        }

        public string[] Nodes { get; set; }

        public double RequestedPublishingInterval { get; set; }

        public uint RequestedMaxKeepAliveCount { get; set; }

        public uint RequestedLifetimeCount { get; set; }

        public bool PublishingEnabled { get; set; }

        public delegate void DataChangedEvent(SubscriptionData[] data);

        public event DataChangedEvent DataChanged;

        internal void Trigger(SubscriptionData[] data)
        {
            DataChanged?.Invoke(data);
        }
    }
}
