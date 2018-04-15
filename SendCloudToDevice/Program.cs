using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;

namespace SendCloudToDevice
{
    class Program
    {
        static ServiceClient serviceClient;
        static string connectionString = "HostName=calgary1.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=Access key here";
        static string deviceName = "mySimulatedDevice";

        static async Task Main(string[] args)
        {
            Console.WriteLine("Send Cloud-to-Device message\n");
            serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
            var t = new Task(async () => await ReceiveFeedbackAsync());
            t.Start();
            Console.WriteLine("Press any key to send a C2D message.");
            while (true)
            {
                Console.ReadLine();
                SendCloudToDeviceMessageAsync().Wait();
            }

        }

        static async Task SendCloudToDeviceMessageAsync()
        {
            var commandMessage = new Message(Encoding.UTF8.GetBytes("Cloud to device message."));
            commandMessage.Ack = DeliveryAcknowledgement.Full;
            await serviceClient.SendAsync(deviceName, commandMessage);
            Console.WriteLine("Message sent.");
        }

        static async Task ReceiveFeedbackAsync()
        {
            //var feedbackReceiver = serviceClient.GetFeedbackReceiver();

            //Console.WriteLine("\nReceiving c2d feedback from service");
            //while (true)
            //{
            //    var feedbackBatch = await feedbackReceiver.ReceiveAsync();
            //    if (feedbackBatch == null) continue;

            //    Console.ForegroundColor = ConsoleColor.Yellow;
            //    Console.WriteLine("Received feedback: {0}", string.Join(", ", feedbackBatch.Records.Select(f => f.StatusCode)));
            //    Console.ResetColor();

            //    await feedbackReceiver.CompleteAsync(feedbackBatch);
            //}
        }
    }
}

