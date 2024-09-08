using Opc.Ua;
using Opc.Ua.Client;

namespace OPC_UA_Client.Services;

public class OpcUaPollingService : IAsyncDisposable
{
    private readonly ILogger<OpcUaPollingService> logger;
    private readonly Dictionary<NodeId, Timer> pollingTimers = new();
    private readonly Lazy<Task<Session>> lazySession;
    private Session session;

    public event Action<NodeId, DataValue> OnDataChanged;

    public OpcUaPollingService(OpcUaSessionProvider sessionProvider, ILogger<OpcUaPollingService> logger)
    {
        this.logger = logger;
        lazySession = new Lazy<Task<Session>>(sessionProvider.GetSessionAsync);
    }

    public async Task StartPollingAsync(NodeId nodeId, int interval = 1000)
    {
        if (pollingTimers.ContainsKey(nodeId))
        {
            logger.LogWarning($"Polling already started for NodeId: {nodeId}");
            return;
        }

        session = await lazySession.Value;

        var timer = new Timer(async _ => await ReadValueAsync(nodeId), null, 0, interval);
        pollingTimers[nodeId] = timer;
    }

    private async Task ReadValueAsync(NodeId nodeId)
    {
        try
        {
            DataValue value = await session.ReadValueAsync(nodeId);

            if (value != null && StatusCode.IsGood(value.StatusCode))
            {
                OnDataChanged?.Invoke(nodeId, value);
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error fetching data for NodeId {nodeId}: {ex.Message}");
        }
    }

    public ValueTask DisposeAsync()
    {
        foreach (var timer in pollingTimers.Values)
        {
            timer.Dispose();
        }

        pollingTimers.Clear();

        return ValueTask.CompletedTask;
    }
}
