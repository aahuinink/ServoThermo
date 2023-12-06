using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using MQTTnet;
using MQTTnet.Client;
using LiveChartsCore.SkiaSharpView.VisualElements;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;
using LiveChartsCore.Defaults;

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
        public double CurrentTemp { get; set; }
        /// <summary>
        /// An array of temperature history
        /// </summary>
        public double[] TempHistory { get; set; }

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
        

        public double CurrentTemp { get; set; }

        IMqttClient client;

        private ObservableCollection<ObservableValue> _temperatureHistory;

        public ObservableCollection<ISeries> Series { get; set; }

        [ObservableProperty]
        public int selectedTemp;

        [ObservableProperty]
        public string connectionStatus;

        [ObservableProperty]
        public string currentTempString;

        [ObservableProperty]
        public string lastReading;

        [ObservableProperty]
        public string selectedTempString;

        [ObservableProperty]
        public string setTempLabel;

        public MainViewModel()
        {
            ConnectionStatus = "Connecting to broker...";
            client = new MqttFactory().CreateMqttClient();          // create mqtt client
            _temperatureHistory = new ObservableCollection<ObservableValue>();  // create place to store temp history values
            Series = new ObservableCollection<ISeries>
        {
            new LineSeries<ObservableValue>
            {
                Values = _temperatureHistory
            }
        }; 

            Connect();
        }
        /// <summary>
        /// Title for the Line Chart
        /// </summary>
        public LabelVisual Title { get; set; } =
            new LabelVisual
            {
                Text = "Temperature History",
                TextSize = 25,
                Padding = new LiveChartsCore.Drawing.Padding(15),
                Paint = new SolidColorPaint(SKColors.Blue)
            };

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

            if (0 == payloadIn.DataType)    // if data reading is an air temperature
            {
                CurrentTemp = payloadIn.CurrentTemp;
                CurrentTempString = $"{CurrentTemp}\u00B0C";
                if (_temperatureHistory.Count >= 30)
                {
                    _temperatureHistory.RemoveAt(0);
                }
                _temperatureHistory.Add(new ObservableValue(CurrentTemp));
            }
            if (1 == payloadIn.DataType)    // if data in is a response to a query
            {
                selectedTemp = (int)payloadIn.CurrentTemp;
                SelectedTempString = $"{selectedTemp}\u00B0C";      // update selected temp

                foreach (double temp in payloadIn.TempHistory)
                {
                    _temperatureHistory.Add(new ObservableValue(temp));
                }

                CurrentTemp = Convert.ToDouble(_temperatureHistory.Last().Value);
                CurrentTempString = $"{CurrentTemp}\u00B0C";
            }
            LastReading = DateTime.Now.ToString();
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
            payloadOut.SetTemp = selectedTemp;
            payloadOut.Query = 0;
            SelectedTempString = $"{selectedTemp}\u00B0C";
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
