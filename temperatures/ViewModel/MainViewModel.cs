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
using System.Timers;

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
        /// <summary>
        /// The temperature to set the thermostat to
        /// </summary>
        public int SetTemp { get; set; }
        public PayloadOut() { }
    }
    public partial class MainViewModel : ObservableObject
    {
        /// <summary>
        /// Timer to make sure that packets are coming every 30 seconds
        /// </summary>
        System.Timers.Timer timer = new System.Timers.Timer();

        DateTime lastRX;
        /// <summary>
        /// The current temperature
        /// </summary>
        public double CurrentTemp { get; set; }

        /// <summary>
        /// MQTT client
        /// </summary>
        IMqttClient client;

        /// <summary>
        /// The temperature history
        /// </summary>
        private ObservableCollection<double> _temperatureHistory;

        /// <summary>
        /// Line series data
        /// </summary>
        public ObservableCollection<ISeries> Series { get; set; }

        /// <summary>
        /// The selected thermostat temperature
        /// </summary>
        [ObservableProperty]
        public int selectedTemp;

        /// <summary>
        /// The connections status
        /// </summary>
        [ObservableProperty]
        public string connectionStatus;

        /// <summary>
        /// The current temperature as a string for data binding
        /// </summary>
        [ObservableProperty]
        public string currentTempString;

        /// <summary>
        /// The time of the last reading
        /// </summary>
        [ObservableProperty]
        public string lastReading;

        /// <summary>
        /// The current selected thermostat temperature
        /// </summary>
        [ObservableProperty]
        public string selectedTempString = $"\u00B0C";

        /// <summary>
        /// X axes object to hold axis data
        /// </summary>
        public ObservableCollection<Axis> XAxes { get; set; }

        /// <summary>
        /// labels for chart axes
        /// </summary>
        public ObservableCollection<string> axisLabels;

        // DEBUGGING STUFF

        [ObservableProperty]
        public int lostRXPackets = 0;

        [ObservableProperty]
        public int lostTXPackets = 0;

        [ObservableProperty]
        public int checksumErrors = 0;

        public MainViewModel()
        {
            timer.Interval = 30000;
            timer.Enabled = true;
            timer.Elapsed += Timer_Elapsed;
            lastRX = DateTime.Now;
            ConnectionStatus = "Connecting to broker...";
            client = new MqttFactory().CreateMqttClient();          // create mqtt client
            _temperatureHistory = new ObservableCollection<double>();  // create place to store temp history values
            Series = new ObservableCollection<ISeries>                  // create data series to display temp history with
        {
            new LineSeries<double>
            {
                Values = _temperatureHistory
            }
        };
            // create axis with labels
            axisLabels = new ObservableCollection<string>();
            XAxes = new ObservableCollection<Axis>
            {
                new Axis
                {
                    Name = "Time",
                    Labels = axisLabels
                }
            };
            Connect();  // connect to the MQTT broker
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            throw new NotImplementedException();
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
                Paint = new SolidColorPaint(SKColors.White)
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
                // connect
                await client.ConnectAsync(connectionOptions);
                // subscribe
                var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                                                .WithTopicFilter("ecet230/AaronH/MISO")
                                                .Build();
                await client.SubscribeAsync(subscribeOptions);
                // message recieved handler
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
            lastRX = DateTime.Now;  // save new RX time
            string rec = arg.ApplicationMessage.ConvertPayloadToString();   // get payload

            // check checksum
            try
            {
                int rxChx = Convert.ToInt32(rec.Substring(0, 3));
                rec = rec.Substring(3);
                int chx = CalculateChecksum(rec);
                if (rxChx != chx)
                {
                    ChecksumErrors++;
                    return Task.CompletedTask;
                }
            }
            catch 
            {
                ChecksumErrors++;
                return Task.CompletedTask;
            }

            // if checksum good
            PayloadIn payloadIn = JsonSerializer.Deserialize<PayloadIn>(rec); // deserialize packet

            if (0 == payloadIn.DataType)    // if data reading is an air temperature
            {
                CurrentTemp = payloadIn.CurrentTemp;
                while (_temperatureHistory.Count > 600) // if greater than 5 hours long (2 readings/min x 300  min)
                {
                    _temperatureHistory.RemoveAt(0);
                }
                _temperatureHistory.Add(CurrentTemp);
            }
            if (1 == payloadIn.DataType)    // if data in is a response to a query
            {
                SelectedTemp = (int)payloadIn.CurrentTemp;

                int itemCount = payloadIn.TempHistory.Length;
                foreach (double temp in payloadIn.TempHistory)
                {
                    itemCount--;
                    axisLabels.Add(DateTime.Now.AddSeconds(-30*itemCount).ToString("HH:mm"));
                    _temperatureHistory.Add(temp);
                }

                CurrentTemp = _temperatureHistory.Last();
                
            }
            CurrentTempString = $"{CurrentTemp}\u00B0C";
            LastReading = DateTime.Now.ToString("HH:mm");
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

            // calculate checksum
            int chx = CalculateChecksum(payloadJson);
            payloadJson = chx.ToString() + payloadJson;     // add checksum to packet

            // build message and publish
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

        /// <summary>
        /// Clears temperature history data and chart
        /// </summary>
        [RelayCommand]
        private void ClearData()
        {
            _temperatureHistory.Clear();
            return;
        }

        private int CalculateChecksum(string payload)
        {
            int chx = 0;
            for (int i = 0; i < payload.Length; i++)
            {
                chx += (byte)payload[i];
            }
            return chx % 1000;
        }
    }

}
