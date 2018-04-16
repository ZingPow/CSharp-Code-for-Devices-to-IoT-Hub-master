using Microsoft.Azure.Devices;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace TempAlert
{
    class TempAlertProcessor : IEventProcessor
    {
        Stopwatch checkpointStopWatch;
        PartitionContext partitionContext;

        public async Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            Trace.TraceInformation(string.Format("EventProcessor Shuting Down.  Partition '{0}', Reason: '{1}'.", this.partitionContext.Lease.PartitionId, reason.ToString()));
            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync();
            }
        }

        public Task OpenAsync(PartitionContext context)
        {
            Trace.TraceInformation(string.Format("Initializing EventProcessor: Partition: '{0}', Offset: '{1}'", context.Lease.PartitionId, context.Lease.Offset));
            this.partitionContext = context;
            this.checkpointStopWatch = new Stopwatch();
            this.checkpointStopWatch.Start();
            return Task.FromResult<object>(null);
        }

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            Trace.TraceInformation("\n");
            Trace.TraceInformation("........ProcessEventsAsync........");
            foreach (EventData eventData in messages)
            {
                try
                {
                    string jsonString = Encoding.UTF8.GetString(eventData.GetBytes());

                    Trace.TraceInformation(string.Format("Message received at '{0}'. Partition: '{1}'",
                        eventData.EnqueuedTimeUtc.ToLocalTime(), this.partitionContext.Lease.PartitionId));

                    Trace.TraceInformation(string.Format("-->Raw Data: '{0}'", jsonString));

                    SensorEvent newSensorEvent = JsonConvert.DeserializeObject<SensorEvent>(jsonString);

                    Trace.TraceInformation(string.Format("-->Serialized Data: '{0}', '{1}', '{2}', '{3}''",
                        newSensorEvent.time, newSensorEvent.deviceid, newSensorEvent.temperature, newSensorEvent.ledstatus));

                    LEDStatusCommand ledStatusCommand = new LEDStatusCommand();

                    ledStatusCommand.time = newSensorEvent.time;

                    ledStatusCommand.source = "Cloud Service one way notification";

                    //Used for Direct Method call
                    //CloudToDeviceMethod methodInvocation = new CloudToDeviceMethod("DMTurnOnLED") { ResponseTimeout = TimeSpan.FromSeconds(30) }; 

                    if (newSensorEvent.temperature > 21 && newSensorEvent.ledstatus == 0)
                    {
                        ledStatusCommand.ledstatus = 1;
                    }
                    else if (newSensorEvent.temperature <= 21 && newSensorEvent.ledstatus == 1)
                    {
                        ledStatusCommand.ledstatus = 0;
                        //Used for Direct Method call
                        //methodInvocation = new CloudToDeviceMethod("DMTurnOffLED") { ResponseTimeout = TimeSpan.FromSeconds(30) };
                    }

                    var messageString = JsonConvert.SerializeObject(ledStatusCommand);
                    var message = new Message(Encoding.UTF8.GetBytes(messageString));

                    // Issuing alarm to device.
                    Trace.TraceInformation("Issuing alarm to device: '{0}'", newSensorEvent.deviceid);
                    Trace.TraceInformation("New Command Parameter: '{0}'", messageString);

                    // Send a one-way notification to the specified device
                    await WorkerRole.iotHubServiceClient.SendAsync(newSensorEvent.deviceid, message, TimeSpan.FromSeconds(30));

                    //Call to Direct Method on the Device
                    //CloudToDeviceMethodResult r = await WorkerRole.iotHubServiceClient.InvokeDeviceMethodAsync(newSensorEvent.deviceid, methodInvocation);
                    //Trace.TraceInformation("Direct Method result '{0}', Payload '{1}' ", r.Status, r.GetPayloadAsJson());
                }
                catch (Exception ex)
                {
                    Trace.TraceInformation("Error in ProssEventsAsync -- {0}\n", ex.Message);
                }
            }

            await context.CheckpointAsync();
        }
    }
}
