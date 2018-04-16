using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.ServiceBus;
using Newtonsoft.Json;
using System.Text;
using System;
using Microsoft.Azure.Devices;

namespace AzureFunctionAlert.TempAlert
{
    class SensorEvent
    {
        public string time { get; set; }
        public string deviceid { get; set; }
        public double temperature { get; set; }
        public int ledstatus { get; set; }
    }


    class LEDStatusCommand
    {
        public string time { get; set; }
        public int ledstatus { get; set; }
        public string source { get; set; }
    }

    public static class TempAlert
    {
        // In the portal, open up the IoT Hub, then "Shared Access Policies" and copy the primary connection string
        // for the "service" access policy and paste it in below
        static string connectionString = "HostName=YourIoTHub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=JustCopyTheWholeConnectionString";

        static ServiceClient serviceClient;

        [FunctionName("TempAlert")]
        public static async void Run([EventHubTrigger("alerteventhub", Connection = "AlertEventHub_EVENTHUB")]string myEventHubMessage, TraceWriter log)
        {
            log.Info($"C# Event Hub trigger function processed a message: {myEventHubMessage}");

            try
            {
                SensorEvent sensorEvent = JsonConvert.DeserializeObject<SensorEvent>(myEventHubMessage);

                serviceClient = ServiceClient.CreateFromConnectionString(connectionString);

                log.Info(string.Format("-->Serialized Data: '{0}', '{1}', '{2}', '{3}''",
                        sensorEvent.time, sensorEvent.deviceid, sensorEvent.temperature, sensorEvent.ledstatus));

                LEDStatusCommand ledStatusCommand = new LEDStatusCommand();

                ledStatusCommand.time = sensorEvent.time;

                ledStatusCommand.source = "Azure Function Direct Method Call";

                //Used for Direct Method call
                CloudToDeviceMethod methodInvocation = new CloudToDeviceMethod("DMTurnOnLED") { ResponseTimeout = TimeSpan.FromSeconds(30) };

                if (sensorEvent.temperature > 21 && sensorEvent.ledstatus == 0)
                {
                    ledStatusCommand.ledstatus = 1;
                }
                else if (sensorEvent.temperature <= 21 && sensorEvent.ledstatus == 1)
                {
                    ledStatusCommand.ledstatus = 0;

                    //Used for Direct Method call
                    methodInvocation = new CloudToDeviceMethod("DMTurnOffLED") { ResponseTimeout = TimeSpan.FromSeconds(30) };
                }

                //var messageString = JsonConvert.SerializeObject(ledStatusCommand);
                //var message = new Message(Encoding.UTF8.GetBytes(messageString));

                // Issuing alarm to device.
                //log.Info(string.Format("Issuing alarm to device: '{0}'", sensorEvent.deviceid));
                //log.Info(string.Format("New Command Parameter: '{0}'", messageString));

                // Send a one-way notification to the specified device
                //await serviceClient.SendAsync(sensorEvent.deviceid, message, TimeSpan.FromSeconds(30));

                //Call to Direct Method on the Device
                CloudToDeviceMethodResult r = await serviceClient.InvokeDeviceMethodAsync(sensorEvent.deviceid, methodInvocation);
                log.Info(string.Format("Direct Method result '{0}', Payload '{1}' ", r.Status, r.GetPayloadAsJson()));

            }
            catch (Exception ex)
            {
                //Log any errors that occurred.
                string errmsg = $"An {ex.GetType().Name} exception occurred. {ex.Message}";
                log.Info(errmsg);
            }
        }
    }
}
