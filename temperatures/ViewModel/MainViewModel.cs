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
        System.Timers.Timer RXtimer = new System.Timers.Timer();

        /// <summary>
        /// Timer to make sure that packets are acknowledged within 10 seconds
        /// </summary>
        System.Timers.Timer TXtimer = new System.Timers.Timer();

        /// <summary>
        /// The last time a temperature reading was recieved
        /// </summary>
        DateTime lastRX;

        /// <summary>
        /// The last time a packet was transmitted
        /// </summary>
        DateTime lastTX;

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
        /// <summary>
        /// The number of lost packets sent by the peripheral device
        /// </summary>
        [ObservableProperty]
        public int lostRXPackets = 0;

        /// <summary>
        /// The number of lost packets sent from the dashboard
        /// </summary>
        [ObservableProperty]
        public int lostTXPackets = 0;

        /// <summary>
        /// The number of packets with checksum errors
        /// </summary>
        [ObservableProperty]
        public int checksumErrors = 0;

        /// <summary>
        /// Is debugging information visible?
        /// </summary>
        [ObservableProperty]
        public bool debuggingVisible = false;

        /// <summary>
        /// Text for the show/hide debugging info button
        /// </summary>
        [ObservableProperty]
        public string debugHide_Text = "Show Debugging";

        public MainViewModel()
        { 
            // RX timer setup
            RXtimer.Interval = 30000;
            RXtimer.Enabled = true;
            RXtimer.Elapsed += RXTimer_Elapsed;

            // TX timer setup
            TXtimer.Interval = 1500;
            TXtimer.Enabled = false;
            TXtimer.Elapsed += TXtimer_Elapsed;

            // Connect to MQTT broker
            ConnectionStatus = "Connecting to broker...";
            client = new MqttFactory().CreateMqttClient();          // create mqtt client

            // graph stuffs
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

        private void TXtimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            LostTXPackets++;
            TXtimer.Enabled = false;
        }

        private void RXTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if((DateTime.Now - lastRX) > TimeSpan.FromSeconds(30))  // if its been more than 30s since the last packet has been recieved
            {
                LostRXPackets++;        // increased lost Recieved packet count
            }
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

            // case statement based on data type flag
            switch (payloadIn.DataType)
            {
                // if data is an air temperature reading
                case 0:
                    lastRX = DateTime.Now;          // update last temp receive time
                    CurrentTemp = payloadIn.CurrentTemp;
                    while (_temperatureHistory.Count > 600) // if greater than 5 hours long (2 readings/min x 300  min)
                    {
                        _temperatureHistory.RemoveAt(0);    // remove old readings
                    }
                    _temperatureHistory.Add(CurrentTemp);  // add the new reading
                    break;

                // if data in is a response to a query for temperature history data
                case 1:
                    SelectedTemp = (int)payloadIn.CurrentTemp;              // get current thermostat temperature

                    int itemCount = payloadIn.TempHistory.Length;

                    // parse array into temp history collection and add labels to graph
                    foreach (double temp in payloadIn.TempHistory)
                    {
                        itemCount--;
                        axisLabels.Add(DateTime.Now.AddSeconds(-30 * itemCount).ToString("HH:mm"));
                        _temperatureHistory.Add(temp);
                    }

                    // set current temperature
                    CurrentTemp = _temperatureHistory.Last();
                    break;

                // if acknowledging a packet with a good checksum
                case 2:
                    TXtimer.Enabled = false;    // tx timer not needed
                    break;

                // if acknowledging a packet, but there was a checksum error
                case 3:
                    ChecksumErrors++;
                    TXtimer.Enabled = false;    // tx timer not needed
                    break;
            }

            // update view
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
            // create payload object
            PayloadOut payloadOut = new PayloadOut();
            payloadOut.SetTemp = SelectedTemp;
            payloadOut.Query = 0;

            // JSON stuffs
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

            // start acknowledgement timer
            lastTX = DateTime.Now;
            TXtimer.Enabled = true;
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

        /// <summary>
        /// Calculates the checksum and returns it.
        /// </summary>
        /// <param name="payload">The string to calculate the checksum from</param>
        /// <returns></returns>
        private int CalculateChecksum(string payload)
        {
            int chx = 0;
            for (int i = 0; i < payload.Length; i++)
            {
                chx += (byte)payload[i];
            }
            return chx % 1000;
        }

        [RelayCommand]
        private void ToggleDebug()
        {
            DebuggingVisible = !DebuggingVisible; // toggle debugging visibility
            if(DebuggingVisible)
            {
                DebugHide_Text = "Hide Debugging";
            }
            else
            {
                DebugHide_Text = "Show Debugging";
            }
            return;
        }
    }

}
