using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;

namespace SimulatedDevice
{
    class Program
    {
        static DeviceClient deviceClient;

        static string iotHubUri = "calgary1.azure-devices.net";
        static string deviceKey = "device key here";  //Primary Key
        static string deviceName = "mySimulatedDevice";

        static int ledstatus;  //0 off 1 on

        static async Task Main(string[] args)
        {
            Console.WriteLine("Simulated device\n");
            try
            {
                deviceClient = DeviceClient.Create(iotHubUri, new DeviceAuthenticationWithRegistrySymmetricKey(deviceName, deviceKey), TransportType.Mqtt);

                if (deviceClient != null)
                {
                    Twin MyTwin = await deviceClient.GetTwinAsync();

                    if (MyTwin != null)
                        SetLED(MyTwin.Properties.Desired.Contains("LEDStatus") ? (int)MyTwin.Properties.Desired["LEDStatus"] : 0);

                    deviceClient.SetConnectionStatusChangesHandler(ConnectionStatusChangeHandler);

                    deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null).Wait();

                    var sendMessageTask = new Task(async () => await SendDeviceToCloudMessagesAsync());
                    sendMessageTask.Start();

                    // setup callbacks for direct methods
                    await deviceClient.SetMethodHandlerAsync("DMTurnOnLED", DMTurnOnLED, null);
                    await deviceClient.SetMethodHandlerAsync("DMTurnOffLED", DMTurnOffLED, null);
                    await deviceClient.SetMethodHandlerAsync("DMToggleLED", DMToggleLED, null);

                    await ReceiveC2dAsync();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Device connection error: {0}", e.Message);
            }

            Console.ReadLine();
        }

        static async Task SendDeviceToCloudMessagesAsync()
        {
            double minTemperature = 20;
            DateTime time;
            Random rand = new Random();

            while (true)
            {
                time = DateTime.UtcNow;

                double r = 0.5 - rand.NextDouble();

                //randomly generate out lier data
                if (Math.Abs(r) > .45)
                {
                    r = r * 12;
                }
                else
                {
                    r = 0;
                }

                //generate semi nice data
                double currentTemperature = minTemperature + Math.Sin((time.Minute * 60 + time.Second) / 450.0 * Math.PI) * 3 + r;

                TelemetryData telemetryDataPoint = new TelemetryData
                {
                    DeviceId = deviceName,
                    Time = time.ToString("o"),
                    Temperature = currentTemperature,
                    Latitude = 51.04522,
                    Longitude = -114.063,
                    LEDStatus = ledstatus
                };

                var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
                var message = new Message(Encoding.UTF8.GetBytes(messageString));

                try
                {
                    await deviceClient.SendEventAsync(message);
                    Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, messageString);
                }
                catch (Exception e)
                {
                    Console.WriteLine("D2C exception: {0}", e.Message);
                }

                await Task.Delay(15000);  //send data every 15 seconds so we have enough data for ML
            }
        }

        static async Task ReceiveC2dAsync()
        {
            Console.WriteLine("\nReceiving cloud to device messages from service");
            while (true)
            {
                Message receivedMessage = await deviceClient.ReceiveAsync();
                if (receivedMessage == null) continue;

                string jsonString = Encoding.UTF8.GetString(receivedMessage.GetBytes());

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Received message: {0}", jsonString);
                Console.ResetColor();

                await deviceClient.CompleteAsync(receivedMessage);
            }
        }

        static Task<MethodResponse> DMTurnOnLED(MethodRequest methodRequest, object userContext)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nReceived LED ON direct message");
            Console.ResetColor();
            TurnOnLED();
            string result = "{\"LED\":\"On\"}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }

        static Task<MethodResponse> DMTurnOffLED(MethodRequest methodRequest, object userContext)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nReceived LED OFF direct message");
            Console.ResetColor();
            TurnOffLED();
            string result = "{\"LED\":\"Off\"}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }

        static Task<MethodResponse> DMToggleLED(MethodRequest methodRequest, object userContext)
        {
            string result;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nReceived LED Toggle direct message");
            Console.ResetColor();

            if (ledstatus == 0)
            {
                ledstatus = 1;
                result = "{\"LED\":\"On\"}";
            }
            else
            {
                ledstatus = 0;
                result = "{\"LED\":\"Off\"}";
            }

            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }

        static void TurnOnLED()
        {
            ledstatus = 1;

            UpdateTwin(ledstatus);
        }

        static void TurnOffLED()
        {
            ledstatus = 0;

            UpdateTwin(ledstatus);
        }

        static async void UpdateTwin(int led)
        {
            TwinCollection reportedProperties = new TwinCollection();
            reportedProperties["LEDStatus"] = led;
            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }

        static void ConnectionStatusChangeHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine();
            Console.WriteLine("Connection Status Changed to {0}", status);
            Console.WriteLine("Connection Status Changed Reason is {0}", reason);
            Console.WriteLine();
            Console.ResetColor();
        }

        private static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("desired property change:");
            Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));
            Console.ResetColor();

            if (desiredProperties.Contains("LEDStatus"))
                SetLED((int)desiredProperties["LEDStatus"]);
        }
        static void SetLED(int ledstatus)
        {
            if (ledstatus != 1)
            {
                TurnOffLED();
            }
            else
            {
                TurnOnLED();
            }
        }
    }
}
