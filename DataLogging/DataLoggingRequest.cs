using System.Threading;

namespace LIB.OPC.Client.DataLogging
{
    public class DataLoggingRequest
    {
        public double LoggingInterval { get; set; }

        public string[] Nodes { get; set; }

        public CancellationToken Token { get; set; }

        public delegate void DataLoggedEvent(object[] data);

        public event DataLoggedEvent DataLogged;

        internal void Trigger(object[] data)
        {
            DataLogged?.Invoke(data);
        }
    }
}
