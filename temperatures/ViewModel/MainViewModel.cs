using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MQTTnet;
using MQTTnet.Client;

namespace temperatures.ViewModel
{
    internal class PayloadIn
    {
        /// <summary>
        /// Classifies the temperature reading as the current air temperature (0) or the selected thermostat temperature (1)
        /// </summary>
        public int DataType { get; set; }
        /// <summary>
        /// The temperature reading
        /// </summary>
        public double Temp { get; set; }
    }
    public class PayloadOut
    {
        /// <summary>
        /// Query type:
        /// 0 - no query
        /// 1 - current thermostat temp
        /// </summary>
        public int Query { get; set; }
        public int SetTemp { get; set; }
        public PayloadOut() { }
    }
    public partial class MainViewModel : ObservableObject
    {
        IMqttClient client = new MqttFactory().CreateMqttClient();

        [ObservableProperty]
        public string connectionStatus;

        [ObservableProperty]
        public string currentTemp;

        [ObservableProperty]
        public string lastReading;

        [ObservableProperty]
        public int selectedTemp;

        [ObservableProperty]
        public string setTempLabel;

        public MainViewModel()
        {
            ConnectionStatus = "Connecting to broker...";
            SelectedTemp = 22;
            Connect();
        }

        /// <summary>
        /// Connects client to MQTT broker
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        public async Task Connect()
        {
            if (client.IsConnected) {
                ConnectionStatus = "Connected to Broker successfully...";
                return;
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
                                                .WithTopicFilter("ecet230/AaronH/MISO")
                                                .Build();
                await client.SubscribeAsync(subscribeOptions);
                client.ApplicationMessageReceivedAsync += Client_ApplicationMessageReceivedAsync;
            }
            await ReqCurrentSetTemp();
            return;
        }

        /// <summary>
        /// handles client disconnect event
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>s
        private Task Client_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            ConnectionStatus = "Disconnected from broker.";
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles client connected event
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private Task Client_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            ConnectionStatus = "Connected to broker.";
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles an MQTT message recieved event
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private Task Client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            string rec = arg.ApplicationMessage.ConvertPayloadToString();
            PayloadIn payloadIn = JsonSerializer.Deserialize<PayloadIn>(rec);
            if (0 == payloadIn.DataType)
            {
                CurrentTemp = $"{payloadIn.Temp}\u00B0C";
            }
            else
            {
                SelectedTemp = (int)payloadIn.Temp;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// sends a temp for the uC to set the thermostat to
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task SetTempButton_Pressed()
        {
            PayloadOut payloadOut = new PayloadOut();
            payloadOut.SetTemp = SelectedTemp;
            payloadOut.Query = 0;
            // Thanks to https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to?pivots=dotnet-6-0 for assistance with JsonSerializer
            string payloadJson = JsonSerializer.Serialize(payloadOut);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("ecet230/AaronH/MOSI")
                .WithPayload(payloadJson)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await client.PublishAsync(message);
        } 
        /// <summary>
        /// requests the current temp the uC has the thermostate set to
        /// </summary>
        /// <returns></returns>
        private async Task ReqCurrentSetTemp()
        {
            PayloadOut payloadJson = new PayloadOut();
            payloadJson.SetTemp = 0;
            payloadJson.Query = 1;
            string payload = JsonSerializer.Serialize(payloadJson);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("ecet230/AaronH/MOSI")
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await client.PublishAsync(message);
        }
    }
}
