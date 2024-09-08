using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Opc.Ua;

namespace OPC_UA_Client.Services;

public class OpcUaSessionProvider : IAsyncDisposable
{
    private readonly IConfiguration configuration;
    private readonly UserTokenType userTokenType;
    private Session? session;

    public OpcUaSessionProvider(IConfiguration configuration, UserTokenType userTokenType = UserTokenType.Anonymous)
    {
        this.configuration = configuration;
        this.userTokenType = userTokenType;
    }

    private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

    public async Task<Session> GetSessionAsync()
    {
        await semaphore.WaitAsync();
        
        try
        {
            if (session != null)
            {
                return session;
            }

            return await CreateSessionAsync();
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<Session> CreateSessionAsync()
    {
        ApplicationInstance applicationInstance = await LoadApplicationConfigurationAsync();
        ConfiguredEndpoint configuredEndpoint = GetConfiguredEndpoint(applicationInstance);

        UserIdentity userIdentity = userTokenType switch
        {
            UserTokenType.Anonymous => new UserIdentity(new AnonymousIdentityToken()),
            UserTokenType.UserName => new UserIdentity(configuration["OpcUaServerSettings:UserIdentity:Username"], configuration["OpcUaServerSettings:UserIdentity:Password"]),
            UserTokenType.Certificate => new UserIdentity(await applicationInstance.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.Find()),
            _ => throw new ArgumentException("Invalid user token type", nameof(userTokenType)),
        };

        session = await Session.Create(applicationInstance.ApplicationConfiguration, configuredEndpoint, false, "Blazor OPC UA Client", 60000, userIdentity, null);

        return session;
    }

    private ConfiguredEndpoint GetConfiguredEndpoint(ApplicationInstance applicationInstance)
    {
        string endpointUrl = configuration["OpcUaServerSettings:OpcUaEndpoint"]!;

        var endpoint = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: false);
        var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, EndpointConfiguration.Create(applicationInstance.ApplicationConfiguration));

        return configuredEndpoint;
    }

    private static async Task<ApplicationInstance> LoadApplicationConfigurationAsync()
    {
        string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ClientConfig.xml");

        var applicationInstance = new ApplicationInstance();
        await applicationInstance.LoadApplicationConfiguration(configFilePath, false);

        bool certOk = await applicationInstance.CheckApplicationInstanceCertificate(false, 2048);

        if (!certOk)
        {
            throw new Exception("Application instance certificate invalid or missing.");
        }

        return applicationInstance;
    }

    public async ValueTask DisposeAsync()
    {
        await session?.CloseAsync()!;
        session?.Dispose();
    }
}
