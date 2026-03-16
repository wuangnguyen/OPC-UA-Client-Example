using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpcUaClient.Services;

/// <summary>
/// Service for subscribing to monitored items and handling notifications.
/// </summary>
public class OpcUaSubscriptionService : IAsyncDisposable
{
    private readonly OpcUaSessionProvider sessionProvider;
    private readonly ILogger<OpcUaSubscriptionService> logger;
    private Subscription? subscription;
    private string? activeServerId;

    /// <summary>
    /// Event triggered when data changes for a monitored item.
    /// </summary>
    public event Action<string, DataValue> OnSubscriptionDataChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpcUaSubscriptionService"/> class.
    /// </summary>
    /// <param name="sessionProvider">The session provider.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public OpcUaSubscriptionService(OpcUaSessionProvider sessionProvider, ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger<OpcUaSubscriptionService>();
        this.sessionProvider = sessionProvider;
    }

    /// <summary>
    /// Adds a monitored item to the subscription.
    /// </summary>
    /// <param name="serverId">The server identifier.</param>
    /// <param name="monitoredItem">The monitored item to add.</param>
    public async Task AddMonitoredItemAsync(string serverId, MonitoredItem monitoredItem)
    {
        await EnsureSubscriptionAsync(serverId);

        AddMonitoredItemInternal(monitoredItem);

        await subscription!.ApplyChangesAsync();
    }

    /// <summary>
    /// Adds multiple monitored items to the subscription.
    /// </summary>
    /// <param name="serverId">The server identifier.</param>
    /// <param name="monitoredItems">The monitored items to add.</param>
    public async Task AddMonitoredItemAsync(string serverId, MonitoredItem[] monitoredItems)
    {
        await EnsureSubscriptionAsync(serverId);

        foreach (var monitoredItem in monitoredItems)
        {
            AddMonitoredItemInternal(monitoredItem);
        }

        await subscription!.ApplyChangesAsync();
    }

    /// <summary>
    /// Ensures that a subscription is created.
    /// </summary>
    /// <param name="serverId">The server identifier.</param>
    private async Task EnsureSubscriptionAsync(string serverId)
    {
        activeServerId = serverId;
        var session = await sessionProvider.CreateSessionAsync(serverId);

        if (subscription == null)
        {
            subscription = new Subscription(session.DefaultSubscription);
            session.AddSubscription(subscription);
            await subscription.CreateAsync();
        }
    }

    /// <summary>
    /// Adds a monitored item to the subscription internally.
    /// </summary>
    /// <param name="monitoredItem">The monitored item to add.</param>
    private void AddMonitoredItemInternal(MonitoredItem monitoredItem)
    {
        if (!subscription!.MonitoredItems.Contains(monitoredItem))
        {
            monitoredItem.Notification += OnNotification;
            subscription.AddItem(monitoredItem);
        }
    }

    /// <summary>
    /// Handles notifications for monitored items.
    /// </summary>
    /// <param name="item">The monitored item.</param>
    /// <param name="e">The event arguments.</param>
    private void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        foreach (var value in item.DequeueValues())
        {
            OnSubscriptionDataChanged?.Invoke(item.DisplayName, value);
        }
    }

    /// <summary>
    /// Disposes the service asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (subscription != null)
        {
            foreach (var monitoredItem in subscription.MonitoredItems)
            {
                monitoredItem.Notification -= OnNotification;
            }

            await subscription.DeleteItemsAsync(CancellationToken.None);
            await subscription.DeleteAsync(true);
        }

        subscription = null;
    }
}