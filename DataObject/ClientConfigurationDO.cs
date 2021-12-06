using Workstation.ServiceModel.Ua;

namespace LIB.OPC.Client.DataObject
{
    /// <summary>
    /// Configuration used to initialize the OPC OPCClient
    /// </summary>
    public class ClientConfigurationDO
    {
        /// <summary>
        /// 
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string EndpointUrl { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public IdentityDO UserIdentity { get; set; } = null;

        ///// <summary>
        ///// 
        ///// </summary>
        public DirectoryStore CertificateStore { get; set; } = null;
    }
}
