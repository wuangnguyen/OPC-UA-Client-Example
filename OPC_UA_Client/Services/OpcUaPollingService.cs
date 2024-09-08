using Opc.Ua;
using Opc.Ua.Client;

namespace OPC_UA_Client.Services;

public class OpcUaPollingService : IAsyncDisposable
{
    private readonly ILogger<OpcUaPollingService> logger;
    private readonly Lazy<Task<Session>> lazySession;
    private Timer? pollingTimer;
    private Session session;
    private NodeId[]? nodeIds;

    public event Action<NodeId, DataValue> OnDataChanged;

    public OpcUaPollingService(OpcUaSessionProvider sessionProvider, ILogger<OpcUaPollingService> logger)
    {
        this.logger = logger;
        lazySession = new Lazy<Task<Session>>(sessionProvider.GetSessionAsync);
    }

    public async Task StartPollingAsync(NodeId nodeId, int interval = 1000)
    {
        await StartPollingAsync([nodeId], interval);
    }

    public async Task StartPollingAsync(NodeId[] nodeIds, int interval = 1000)
    {
        session = await lazySession.Value;
        
        this.nodeIds = nodeIds;

        pollingTimer = new Timer(async _ => await ReadValuesAsync(), null, 0, interval);
    }

    private async Task ReadValuesAsync()
    {
        if (nodeIds == null || nodeIds.Length == 0)
        {
            return;
        }

        try
        {
            var (readValues, results) = await session.ReadValuesAsync(nodeIds);

            for (int i = 0; i < nodeIds.Length; i++)
            {
                var value = readValues[i];
                if (value != null && StatusCode.IsGood(value.StatusCode))
                {
                    OnDataChanged?.Invoke(nodeIds[i], value);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error fetching data for NodeIds: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (pollingTimer != null)
        {
            await pollingTimer.DisposeAsync();
        }
    }
}
