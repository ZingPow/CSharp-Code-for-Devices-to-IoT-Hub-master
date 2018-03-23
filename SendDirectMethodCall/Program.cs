using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;

namespace SendDirectMethodCall
{
    class LEDStatusCommand
    {
        public string time { get; set; }
        public int ledstatus { get; set; }
        public string source { get; set; }
    }

    class Program
    {
        static ServiceClient serviceClient;
        static string connectionString = "HostName=YourIoTHub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=JustCopyTheWholeConnectionString";
        static string deviceName = "mySimulatedDevice";

        static void Main(string[] args)
        {
            Console.WriteLine("Send Cloud-to-Device message\n");
            serviceClient = ServiceClient.CreateFromConnectionString(connectionString);

            Console.WriteLine("Press any key to send a C2D direct message.");
            while (true)
            {
                Console.ReadLine();
                SendCloudToDeviceDirectAsync().Wait();
            }
        }
        private async static Task SendCloudToDeviceDirectAsync()
        {
            var commandMessage = new Message(Encoding.ASCII.GetBytes("Cloud to device direct."));
            commandMessage.Ack = DeliveryAcknowledgement.Full;

            LEDStatusCommand ledStatusCommand = new LEDStatusCommand();

            ledStatusCommand.time = DateTime.Now.ToString();

            ledStatusCommand.source = "Azure Function Direct Method Call";

            CloudToDeviceMethod methodInvocation = new CloudToDeviceMethod("DMToggleLED") { ResponseTimeout = TimeSpan.FromSeconds(30) };

            CloudToDeviceMethodResult r = await serviceClient.InvokeDeviceMethodAsync(deviceName, methodInvocation);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(string.Format("Direct Method result '{0}', Payload '{1}' ", r.Status, r.GetPayloadAsJson()));
            Console.ResetColor();
        }
    }
}

