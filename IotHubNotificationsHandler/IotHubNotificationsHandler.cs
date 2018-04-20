using System;
using System.Net.Http;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

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
        // Use a static HttpClient and EventHubClient so that multiple function executions within a single function app instance 
        // don't have to initialize new resources every execution.
        static readonly HttpClient client = new HttpClient();

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

            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var kvClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback), IotHubNotificationsHandler.client);
            var secretUri = Environment.GetEnvironmentVariable("twilio-secret");
            var twilioSettings = (await kvClient.GetSecretAsync(secretUri)).Value;
            var settings = TwilioSettings.DeserializeTwilioSettings(twilioSettings);

            var accountSid = settings.AccountSid;
            var authToken = settings.AuthToken;
            var to = new PhoneNumber(settings.To);
            var from = new PhoneNumber(settings.From);

            var twilioClient = new TwilioRestClient(accountSid, authToken);

            foreach (var @event in events)
            {
                var deviceId = ((JObject) @event.Data)["deviceId"];
                var deviceStatus = ((JObject) @event.Data)["opType"].ToString().Replace("Device", string.Empty);
                var hub = ((JObject) @event.Data)["hubName"];

                var message = await MessageResource.CreateAsync(
                    to: to,
                    from: from,
                    body: $"Device ID: {deviceId} was {deviceStatus} (hub: {hub})",
                    client: twilioClient);

                log.Info($"Event: device ID: {deviceId} was {deviceStatus} (hub: {hub}). Twilio status: {message.Status}.");
            }
            return new JsonResult(new { result = "ok" });
        }
    }
}
