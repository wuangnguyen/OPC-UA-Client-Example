using Microsoft.Extensions.Options;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Opc.Ua;
using OPC_UA_Client.Models;

namespace OPC_UA_Client.Services;

/// <summary>
/// Provides an instance of communication session.
/// </summary>
public class OpcUaSessionProvider : IAsyncDisposable
{
    private readonly OpcUaSettings opcUaSettings;
    private Session? session;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpcUaSessionProvider"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    public OpcUaSessionProvider(IOptions<OpcUaSettings> options)
    {
        opcUaSettings = options.Value;
    }

    private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Create an communication session asynchronously.
    /// </summary>
    /// <returns>The communication session.</returns>
    public async Task<Session> CreateSessionAsync()
    {
        await semaphore.WaitAsync();
        
        try
        {
            if (session != null)
            {
                return session;
            }

            return await CreateSessionInternalAsync();
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Creates a new communication session asynchronously.
    /// </summary>
    /// <returns>The created session.</returns>
    private async Task<Session> CreateSessionInternalAsync()
    {
        ApplicationInstance applicationInstance = await LoadApplicationConfigurationAsync();
        ConfiguredEndpoint configuredEndpoint = GetConfiguredEndpoint(applicationInstance);

        UserIdentity userIdentity = opcUaSettings.UserTokenType switch
        {
            UserTokenType.Anonymous => new UserIdentity(new AnonymousIdentityToken()),
            UserTokenType.UserName => new UserIdentity(opcUaSettings.UserIdentity.Username, opcUaSettings.UserIdentity.Password),
            UserTokenType.Certificate => new UserIdentity(await applicationInstance.ApplicationConfiguration.SecurityConfiguration.ApplicationCertificate.Find()),
            _ => throw new ArgumentException($"{opcUaSettings.UserTokenType.ToString()} is invalid user token type")
        };

        session = await Session.Create(applicationInstance.ApplicationConfiguration, configuredEndpoint, false, opcUaSettings.ApplicationName, 60000, userIdentity, null);

        return session;
    }

    /// <summary>
    /// Gets the configured endpoint.
    /// </summary>
    /// <param name="applicationInstance">The application instance.</param>
    /// <returns>The configured endpoint.</returns>
    private ConfiguredEndpoint GetConfiguredEndpoint(ApplicationInstance applicationInstance)
    {
        string endpointUrl = opcUaSettings.ServerEndpoint;

        var endpoint = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: false);
        var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, EndpointConfiguration.Create(applicationInstance.ApplicationConfiguration));

        return configuredEndpoint;
    }

    /// <summary>
    /// Loads the application configuration asynchronously.
    /// </summary>
    /// <returns>The application instance.</returns>
    private async Task<ApplicationInstance> LoadApplicationConfigurationAsync()
    {
        string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, opcUaSettings.ApplicationConfigFilePath);

        var applicationInstance = new ApplicationInstance();
        await applicationInstance.LoadApplicationConfiguration(configFilePath, false);

        bool certOk = await applicationInstance.CheckApplicationInstanceCertificate(false, 2048);

        if (!certOk)
        {
            throw new Exception("Application instance certificate invalid or missing.");
        }

        return applicationInstance;
    }

    /// <summary>
    /// Disposes the service asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await session?.CloseAsync()!;
        session?.Dispose();
    }
}
