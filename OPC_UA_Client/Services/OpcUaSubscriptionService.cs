using Opc.Ua;
using Opc.Ua.Client;

namespace OPC_UA_Client.Services;

public class OpcUaSubscriptionService : IAsyncDisposable
{
    private readonly Lazy<Task<Session>> lazySession;
    private readonly ILogger<OpcUaSubscriptionService> logger;
    private Subscription? subscription;

    public event Action<string, DataValue> OnSubscriptionDataChanged;

    public OpcUaSubscriptionService(OpcUaSessionProvider sessionProvider, ILogger<OpcUaSubscriptionService> logger)
    {
        this.logger = logger;
        lazySession = new Lazy<Task<Session>>(sessionProvider.GetSessionAsync);
    }

    public async Task AddMonitoredItemAsync(MonitoredItem monitoredItem)
    {
        await EnsureSubscriptionAsync();

        AddMonitoredItemInternal(monitoredItem);

        await subscription!.ApplyChangesAsync();
    }

    public async Task AddMonitoredItemAsync(MonitoredItem[] monitoredItems)
    {
        await EnsureSubscriptionAsync();

        foreach (var monitoredItem in monitoredItems)
        {
            AddMonitoredItemInternal(monitoredItem);
        }

        await subscription!.ApplyChangesAsync();
    }

    private async Task EnsureSubscriptionAsync()
    {
        var session = await lazySession.Value;

        if (subscription == null)
        {
            subscription = new Subscription(session.DefaultSubscription);
            session.AddSubscription(subscription);
            await subscription.CreateAsync();
        }
    }

    private void AddMonitoredItemInternal(MonitoredItem monitoredItem)
    {
        if (!subscription!.MonitoredItems.Contains(monitoredItem))
        {
            monitoredItem.Notification += OnNotification;
            subscription.AddItem(monitoredItem);
        }
    }

    private void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        foreach (var value in item.DequeueValues())
        {
            OnSubscriptionDataChanged?.Invoke(item.DisplayName, value);
        }
    }

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