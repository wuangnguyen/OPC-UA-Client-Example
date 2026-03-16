using Microsoft.Extensions.Options;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Opc.Ua;
using OpcUaClient.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace OpcUaClient.Services;

/// <summary>
/// Provides a communication session for a specified server. The session is cached for subsequent calls.
/// </summary>
public class OpcUaSessionProvider : IAsyncDisposable
{
    private readonly List<OpcUaSettings> opcUaSettings;
    private readonly Dictionary<string, Session> sessions = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OpcUaSessionProvider"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    public OpcUaSessionProvider(IOptions<List<OpcUaSettings>> options)
    {
        opcUaSettings = options.Value;
    }

    private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Creates a communication session for the specified server. The session is cached for subsequent calls.
    /// </summary>
    /// <param name="serverId">The server identifier.</param>
    /// <returns>The communication session.</returns>
    /// <exception cref="ArgumentException">Thrown when serverId is null or empty, or when no matching server is found.</exception>
    public async Task<Session> CreateSessionAsync(string serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId))
        {
            throw new ArgumentException("Server ID must be provided.", nameof(serverId));
        }

        await semaphore.WaitAsync();

        try
        {
            if (sessions.TryGetValue(serverId, out var existingSession))
            {
                return existingSession;
            }

            var settings = opcUaSettings.FirstOrDefault(s => s.ServerId == serverId)
                ?? throw new ArgumentException($"No OPC UA server found with ServerId '{serverId}'.", nameof(serverId));

            return await CreateSessionInternalAsync(serverId, settings);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Creates a new communication session.
    /// </summary>
    /// <param name="serverId">The server identifier.</param>
    /// <param name="opcUaSettings">The OPC UA settings to use.</param>
    /// <returns>The created session.</returns>
    private async Task<Session> CreateSessionInternalAsync(string serverId, OpcUaSettings opcUaSettings)
    {
        ApplicationInstance applicationInstance = await LoadApplicationConfigurationAsync(opcUaSettings);
        ConfiguredEndpoint configuredEndpoint = GetConfiguredEndpoint(applicationInstance, opcUaSettings);

        UserIdentity userIdentity = opcUaSettings.UserTokenType switch
        {
            UserTokenType.Anonymous => new UserIdentity(new AnonymousIdentityToken()),
            UserTokenType.UserName => new UserIdentity(opcUaSettings.UserIdentity.Username, opcUaSettings.UserIdentity.Password),
            _ => throw new ArgumentException($"{opcUaSettings.UserTokenType.ToString()} is not yet supported")
        };

        var applicationConfiguration = applicationInstance.ApplicationConfiguration;

        var session = await Session.Create(
            applicationConfiguration,
            configuredEndpoint,
            true,
            applicationConfiguration.ApplicationName,
            (uint)applicationConfiguration.ClientConfiguration.DefaultSessionTimeout,
            userIdentity, null
        );

        sessions[serverId] = session;

        return session;
    }

    /// <summary>
    /// Gets the configured endpoint.
    /// </summary>
    /// <param name="applicationInstance">The application instance.</param>
    /// <param name="opcUaSettings">The OPC UA settings.</param>
    /// <returns>The configured endpoint.</returns>
    private ConfiguredEndpoint GetConfiguredEndpoint(ApplicationInstance applicationInstance, OpcUaSettings opcUaSettings)
    {
        string endpointUrl = opcUaSettings.ServerEndpoint;

        // TODO: get EndpointDescription from the injected input
        var endpoint = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: false);
        var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, EndpointConfiguration.Create(applicationInstance.ApplicationConfiguration));

        return configuredEndpoint;
    }

    /// <summary>
    /// Loads the application configuration asynchronously.
    /// </summary>
    /// <param name="opcUaSettings">The OPC UA settings.</param>
    /// <returns>The application instance.</returns>
    private async Task<ApplicationInstance> LoadApplicationConfigurationAsync(OpcUaSettings opcUaSettings)
    {
        string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, opcUaSettings.ApplicationConfigurationFilePath);

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
        foreach (var session in sessions.Values)
        {
            await session.CloseAsync();
            session.Dispose();
        }

        sessions.Clear();
    }
}
