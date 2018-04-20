namespace IotHubNotifications
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    class TwilioSettings
    {
        public string AccountSid { get; set; }
        public string AuthToken { get; set; }
        public string To { get; set; }
        public string From { get; set; }

        public static TwilioSettings DeserializeTwilioSettings(string payload)
        {
            return JsonConvert.DeserializeObject<TwilioSettings>(payload, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
        }
    }
}