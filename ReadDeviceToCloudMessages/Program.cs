using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.ServiceBus.Messaging;
using System.Threading;

namespace ReadDeviceToCloudMessages
{
    class Program
    {
        static string connectionString = "HostName=YourIoTHub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=JustCopyTheWholeConnectionString";
        static string iotHubD2cEndpoint = "messages/events";
        static EventHubClient eventHubClient;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Receive messages. Ctrl-C to exit.\n");
            eventHubClient = EventHubClient.CreateFromConnectionString(connectionString, iotHubD2cEndpoint);

            var d2cPartitions = eventHubClient.GetRuntimeInformation().PartitionIds;

            CancellationTokenSource cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine("Exiting...");
            };

            var tasks = new List<Task>();
            foreach (string partition in d2cPartitions)
            {
                tasks.Add(ReceiveMessagesFromDeviceAsync(partition, cts.Token));
            }
            await Task.WhenAll(tasks.ToArray());
        }

        private static async Task ReceiveMessagesFromDeviceAsync(string partition, CancellationToken ct)
        {
            var eventHubReceiver = await eventHubClient.GetDefaultConsumerGroup().CreateReceiverAsync(partition, DateTime.UtcNow);
            while (true)
            {
                if (ct.IsCancellationRequested)
                    break;

                EventData eventData = await eventHubReceiver.ReceiveAsync();
                if (eventData == null) continue;

                string data = Encoding.UTF8.GetString(eventData.GetBytes());
                Console.WriteLine($"Message received. Partition: {partition} Data: '{data}'");
            }
        }
    }
}
