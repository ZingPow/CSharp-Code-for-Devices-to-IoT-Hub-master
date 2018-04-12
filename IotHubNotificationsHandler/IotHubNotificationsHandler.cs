namespace IotHubNotifications
{
    using System.IO;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.WebJobs.Host;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    public static class IotHubNotificationsHandler
    {
        [FunctionName("IotHubNotificationsHandler")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function received a request.");

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var events = EventGridEvent.DeserializeEventGrid(requestBody);

            if (events.Length == 1 && events[0].EventType == "Microsoft.EventGrid.SubscriptionValidationEvent")
            {
                var validationCode = (string)((JObject)events[0].Data)["validationCode"];

                log.Info($"Subscription validation event: {validationCode}");

                return new JsonResult(new { validationResponse = validationCode });
            }

            foreach (var @event in events)
            {
                log.Info($"Event: {@event.EventType} for device ID: {((JObject)events[0].Data)["deviceId"]}");
            }
            return new JsonResult(new { result = "ok" });
        }
    }
}
