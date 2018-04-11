using System;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;

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
            deviceClient = DeviceClient.Create(iotHubUri, new DeviceAuthenticationWithRegistrySymmetricKey(deviceName, deviceKey), TransportType.Mqtt_WebSocket_Only);

            var sendMessageTask = new Task(async () => await SendDeviceToCloudMessagesAsync());
            sendMessageTask.Start();

            // setup callbacks for direct methods
            await deviceClient.SetMethodHandlerAsync("DMTurnOnLED", DMTurnOnLED, null);
            await deviceClient.SetMethodHandlerAsync("DMTurnOffLED", DMTurnOffLED, null);
            await deviceClient.SetMethodHandlerAsync("DMToggleLED", DMToggleLED, null);

            await ReceiveC2dAsync();
            Console.ReadLine();
        }
        static async Task SendDeviceToCloudMessagesAsync()
        {
            double minTemperature = 15;
            Random rand = new Random();

            while (true)
            {
                double currentTemperature = minTemperature + rand.NextDouble() * 15;

                TelemetryData telemetryDataPoint = new TelemetryData
                {
                    DeviceId = deviceName,
                    Time = DateTime.UtcNow.ToString("o"),
                    Temperature = currentTemperature,
                    LEDStatus = ledstatus
                };

                var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
                var message = new Message(Encoding.UTF8.GetBytes(messageString));
                
                await deviceClient.SendEventAsync(message);
                Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, messageString);

                await Task.Delay(30000);
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
        }

        static void TurnOffLED()
        {
            ledstatus = 0;
        }

    }
}
