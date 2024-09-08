using Opc.Ua;

namespace OpcUaClient.Models
{
    public class OpcUaSettings
    {
        public string ServerEndpoint { get; set; } = string.Empty;
        public UserTokenType UserTokenType { get; set; } = UserTokenType.Anonymous;
        public string ApplicationName { get; set; } = "Blazor OPC UA Client";
        public string ApplicationConfigFilePath { get; set; } = "ApplicationConfig.xml";
        public UserIdentitySettings UserIdentity { get; set; } = new UserIdentitySettings();
    }

    public class UserIdentitySettings
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string PathToCertificate { get; set; } = string.Empty;
    }
}