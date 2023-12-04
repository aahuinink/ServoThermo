using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MQTTnet;
using MQTTnet.Client;

namespace temperatures.ViewModel
{
    public partial class MainViewModel : ObservableObject
    {
        IMqttClient client = new MqttFactory().CreateMqttClient();

        [ObservableProperty]
        public string connectionStatus;

        [ObservableProperty]
        public string currentTemp;

        [ObservableProperty]
        public string lastReading;

        public MainViewModel()
        {
            Connect();
        }

        [RelayCommand]
        public async Task Connect()
        {
            if (client.IsConnected) {
                await client.DisconnectAsync();
            }
            else
            {
                var connectionOptions = new MqttClientOptionsBuilder()
                                                .WithClientId(Guid.NewGuid().ToString())
                                                .WithTcpServer("broker.emqx.io", 1883)
                                                .WithCleanSession()
                                                .Build();
                client.ConnectedAsync += Client_ConnectedAsync;
                client.DisconnectedAsync += Client_DisconnectedAsync;
                await client.ConnectAsync(connectionOptions);
                var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                                                .WithTopicFilter("ecet230/AaronH/temperatures")
                                                .Build();
                await client.SubscribeAsync(subscribeOptions);
                client.ApplicationMessageReceivedAsync += Client_ApplicationMessageReceivedAsync;
            }
            return;
        }

        private Task Client_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            ConnectionStatus = "Disconnected from broker.";
            return Task.CompletedTask;
        }

        private Task Client_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            ConnectionStatus = "Connected to broker.";
            return Task.CompletedTask;
        }

        private Task Client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            CurrentTemp = $"{arg.ApplicationMessage.ConvertPayloadToString()} deg Celcius";
            LastReading = DateTime.Now.ToString();
            return Task.CompletedTask;
        }
    }
}
