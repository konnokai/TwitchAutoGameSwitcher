using LaunchDarkly.EventSource;
using System.Reflection;

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

            var configurationBuilder = Configuration.Builder(new Uri(_endpoint));
            configurationBuilder.RequestHeader("User-Agent", $"TwitchAutoGameSwitcher/{Assembly.GetExecutingAssembly().GetName().Version} (+https://github.com/konnokai/TwitchAutoGameSwitcher)");

            _eventSource = new EventSource(configurationBuilder.Build());

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

        public void Stop()
        {
            _eventSource.Close();
        }

        public void Dispose()
        {
            _eventSource.Dispose();
        }
    }
}