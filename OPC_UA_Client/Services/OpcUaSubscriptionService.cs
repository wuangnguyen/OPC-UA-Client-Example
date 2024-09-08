using Opc.Ua;
using Opc.Ua.Client;

namespace OPC_UA_Client.Services;
public class OpcUaSubscriptionService : IAsyncDisposable
{
    private readonly Lazy<Task<Session>> lazySession;
    private readonly ILogger<OpcUaSubscriptionService> logger;
    private readonly List<Subscription> subscriptions = new();

    public event Action<string, DataValue> OnSubscriptionDataChanged;

    public OpcUaSubscriptionService(OpcUaSessionProvider sessionProvider, ILogger<OpcUaSubscriptionService> logger)
    {
        this.logger = logger;
        lazySession = new Lazy<Task<Session>>(sessionProvider.GetSessionAsync);
    }

    public async Task AddSubscriptionAsync(MonitoredItem monitoredItem)
    {
        try
        {
            var session = await lazySession.Value;

            var subscription = new Subscription(session.DefaultSubscription);
            monitoredItem.Notification += OnNotification;
            subscription.AddItem(monitoredItem);
            session.AddSubscription(subscription);
            subscription.Create();
            subscriptions.Add(subscription);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting up subscription");
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
        foreach (var subscription in subscriptions)
        {
            subscription.Delete(true);
        }

        subscriptions.Clear();
        
        await ValueTask.CompletedTask;
    }
}