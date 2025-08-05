using LaunchDarkly.EventSource;

namespace TwitchAutoGameSwitcher.NxApi
{
    public class SSEClient : IDisposable
    {
        private readonly string _endpoint;
        private readonly EventSource _eventSource;

        public event Action<string, string>? OnEventReceived;

        public SSEClient(string endpoint)
        {
            _endpoint = endpoint;
            var config = Configuration.Builder(new Uri(_endpoint)).Build();
            _eventSource = new EventSource(config);

            _eventSource.MessageReceived += (sender, e) =>
            {
                // e.EventName: event type, e.Message.Data: event data
                OnEventReceived?.Invoke(e.EventName, e.Message.Data);
            };
        }

        public void Start()
        {
            Task.Run(() => _eventSource.StartAsync());
        }

        public void Dispose()
        {
            _eventSource.Dispose();
        }
    }
}