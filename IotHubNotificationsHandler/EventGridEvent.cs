namespace IotHubNotifications
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    class EventGridEvent
    {
        public string Id { get; set; }
        public string Topic { get; set; }
        public string Subject { get; set; }
        public string EventType { get; set; }
        public string EventTime { get; set; }
        public object Data { get; set; }

        public static EventGridEvent[] DeserializeEventGrid(string payload)
        {
            return JsonConvert.DeserializeObject<EventGridEvent[]>(payload, new JsonSerializerSettings {ContractResolver = new CamelCasePropertyNamesContractResolver()});
        }
    }
}