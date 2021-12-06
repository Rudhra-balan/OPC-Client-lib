using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LIB.OPC.Client.DataLogging;
using LIB.OPC.Client.DataObject;
using LIB.OPC.Client.Enumeration;
using LIB.OPC.Client.Subscribe;
using log4net;
using Workstation.ServiceModel.Ua;
using Workstation.ServiceModel.Ua.Channels;

namespace LIB.OPC.Client
{
    public class OpcClient : IDisposable
    {
        public static OpcClient Instance = null;
        private readonly ICertificateStore _certificateStore;
        private ApplicationDescription _appDescription;

        private UaTcpSessionChannel _channel;

        private ClientConfigurationDO _configuration;

        private EndpointDescription _selectedEndpoint;

        private IUserIdentity _userIdentity;

        public delegate void ConnectChangedEvent (Status status);

        public event ConnectChangedEvent ConnectChanged;


        public OpcClient()
        {
            SysLog = LogManager.GetLogger(typeof(OpcClient));
            _configuration = null;
             
            _certificateStore = null;

            _channel = null;

            _selectedEndpoint = null;

            _userIdentity = new AnonymousIdentity();

            ConnectionStatus = Status.Disconnected;
        }

        public ILog SysLog { get; }


        public Status ConnectionStatus { get; set; }

        #region IDisposable Methods

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        #endregion

        /// <summary>
        ///     Performs agent actions depending on the command received.
        /// </summary>
        /// <param name="ipAddress"></param>
        public static OpcClient Start(string ipAddress)
        {
            var client = new OpcClient();
            try
            {
                client.Initialize(new ClientConfigurationDO
                {
                    ApplicationName = "OPCClient",
                    CertificateStore = null,
                    UserIdentity = null,
                    EndpointUrl = $"opc.tcp://{ipAddress}:4840"
                });

                client.OpenAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return client;
        }

        public void Initialize(ClientConfigurationDO configuration)
        {
            _configuration = configuration;

            _appDescription = new ApplicationDescription
            {
                ApplicationName = configuration.ApplicationName,
                ApplicationType = ApplicationType.Client
            };


            if (configuration.UserIdentity != null)
                _userIdentity = new UserNameIdentity(configuration.UserIdentity.UserName,
                    configuration.UserIdentity.Password);
        }

        public async Task OpenAsync()
        {
            try
            {
                var getEndpointsRequest = new GetEndpointsRequest
                {
                    EndpointUrl = _configuration.EndpointUrl,
                    ProfileUris = new[] {TransportProfileUris.UaTcpTransport}
                };

                var getEndpointsResponse = await UaTcpDiscoveryService.GetEndpointsAsync(getEndpointsRequest);

                if (_certificateStore != null)
                {
                    //To Do
                }
                else
                {
                    _selectedEndpoint =
                        getEndpointsResponse.Endpoints.FirstOrDefault(e =>
                            e.SecurityPolicyUri == SecurityPolicyUris.None);

                    if (_selectedEndpoint != null)
                    {
                        _selectedEndpoint.EndpointUrl = _configuration.EndpointUrl;

                        _channel = new UaTcpSessionChannel(_appDescription, null,
                            _userIdentity, _selectedEndpoint);
                    }
                }

                _channel.Closed += Channel_Closed;
                _channel.Opened += Channel_Opened;

                _channel.Faulted += Channel_Faulted;

                await _channel.OpenAsync();
            }
            catch (Exception ex)
            {
                SysLog.Info($"An Error Occured {typeof(OpcClient)}_{MethodBase.GetCurrentMethod()} {ex.Message}");
            }
        }

        public async Task CloseAsync()
        {
            try
            {
                if (_channel != null && !await _channel.IsEmpty())
                {
                    await _channel.CloseAsync();

                    _channel.Closed += Channel_Closed;
                }
            }
            catch (Exception ex)
            {
                SysLog.Info($"An Error Occured {typeof(OpcClient)}_{MethodBase.GetCurrentMethod()} {ex.Message}");
            }
        }

        public async Task<List<object>> ReadValueAsync(List<string> ids)
        {
            try
            {
                if (ConnectionStatus != Status.Connected)
                    throw new InvalidDataException("Not Connected");

                var readValueIds = new List<ReadValueId>();

                foreach (var id in ids)
                    readValueIds.Add(new ReadValueId
                    {
                        NodeId = NodeId.Parse(id),
                        AttributeId = AttributeIds.Value
                    });

                var request = new ReadRequest
                {
                    NodesToRead = readValueIds.ToArray()
                };

                var response = await _channel.ReadAsync(request);

                var values = new List<object>();

                foreach (var dataValue in response.Results)
                    values.Add(StatusCode.IsGood(dataValue.StatusCode) ? dataValue.Value : null);

                return values;
            }
            catch (Exception ex)
            {
                SysLog.Info($"An Error Occured {typeof(OpcClient)}_{MethodBase.GetCurrentMethod()} {ex.Message}");
            }

            return null;
        }


        public async Task<List<uint>> WriteValueAsync(Dictionary<string, object> idsValues)
        {
            try
            {
                if (ConnectionStatus != Status.Connected)
                    throw new InvalidDataException("Not Connected");

                var request = new WriteRequest
                {
                    NodesToWrite = idsValues.Select(id => new WriteValue
                    {
                        NodeId = NodeId.Parse(id.Key), Value = new DataValue(id.Value), AttributeId = AttributeIds.Value
                    }).ToArray()
                };

                var response = await _channel.WriteAsync(request);

                return response.Results.Select(statusCode => statusCode.Value).ToList();
            }
            catch (Exception ex)
            {
                SysLog.Info($"An Error Occured {typeof(OpcClient)}_{MethodBase.GetCurrentMethod()} {ex.Message}");
            }

            return null;
        }

        public async Task SubscribeAsync(SubscribeRequest request)
        {
            try
            {
                var subscriptionRequest = new CreateSubscriptionRequest
                {
                    RequestedPublishingInterval = request.RequestedPublishingInterval,
                    RequestedMaxKeepAliveCount = request.RequestedMaxKeepAliveCount,
                    RequestedLifetimeCount = request.RequestedLifetimeCount,
                    PublishingEnabled = request.PublishingEnabled
                };

                var subscriptionResponse = await _channel.CreateSubscriptionAsync(subscriptionRequest);

                var id = subscriptionResponse.SubscriptionId;

                var itemsRequest = new CreateMonitoredItemsRequest
                {
                    SubscriptionId = id,
                    ItemsToCreate = request.Nodes.Select(node => new MonitoredItemCreateRequest
                    {
                        ItemToMonitor = new ReadValueId {NodeId = NodeId.Parse(node), AttributeId = AttributeIds.Value},
                        MonitoringMode = MonitoringMode.Reporting
                    }).ToArray()
                };

                var itemResponse = await _channel.CreateMonitoredItemsAsync(itemsRequest);


                var notificationResponse = new Action<PublishResponse>(publishResponse =>
                {
                    foreach (var dataChangeNotification in publishResponse.NotificationMessage.NotificationData
                        .OfType<DataChangeNotification>())
                        request.Trigger(dataChangeNotification.MonitoredItems.Select(item => new SubscriptionData
                        {
                            SubscriptionId = publishResponse.SubscriptionId,
                            ClientHandle = item.ClientHandle,
                            DataValue = item.Value,
 
                        }).ToArray());
                });

                _channel.Where(response => response.SubscriptionId == id).Subscribe(notificationResponse);
            }
            catch (Exception ex)
            {
                SysLog.Info($"An Error Occured {typeof(OpcClient)}_{MethodBase.GetCurrentMethod()} {ex.Message}");
            }
        }

        public async Task StartDataLoggingAsync(DataLoggingRequest request)
        {
            var registerNodesRequest = new RegisterNodesRequest
            {
                NodesToRegister = request.Nodes.Select(NodeId.Parse).ToArray()
            };

            var registerNodesResponse = await _channel.RegisterNodesAsync(registerNodesRequest);

            var readRequest = new ReadRequest
            {
                NodesToRead = (registerNodesResponse?.RegisteredNodeIds ?? throw new InvalidOperationException())
                    .Select(n => new ReadValueId {NodeId = n, AttributeId = AttributeIds.Value})
                    .ToArray()
            };

            while (!request.Token.IsCancellationRequested)
            {
                var readResponse = await _channel.ReadAsync(readRequest).ConfigureAwait(false);

                request.Trigger(readResponse.Results.Select(x => x.Value).ToArray());

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(request.LoggingInterval), request.Token);
                }
                catch (Exception ex)
                {
                    SysLog.Info($"An Error Occured {typeof(OpcClient)}_{MethodBase.GetCurrentMethod()} {ex.Message}");
                }
            }
        }

        #region Events

        private void Channel_Closed(object sender, EventArgs e)
        {
            ConnectionStatus = Status.Disconnected;
            ConnectChanged?.Invoke(ConnectionStatus);
        }

        private void Channel_Faulted(object sender, EventArgs e)
        {
            ConnectionStatus = Status.Faulted;
            ConnectChanged?.Invoke(ConnectionStatus);
        }

        private void Channel_Opened(object sender, EventArgs e)
        {
            ConnectionStatus = Status.Connected;
            ConnectChanged?.Invoke(ConnectionStatus);
        }

        #endregion
    }
}