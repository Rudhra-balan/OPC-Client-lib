
namespace LIB.OPC.Client.DataObject
{
    public class IdentityDO
    {
        public IdentityDO(string userName, string password)
        {
            UserName = userName;

            Password = password;
        }

        public string UserName { get; set; }

        public string Password { get; set; }
    }
}
