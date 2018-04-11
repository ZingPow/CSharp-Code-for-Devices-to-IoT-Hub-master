using System.Runtime.Serialization;

namespace SimulatedDevice
{
    class LEDStatusCommand
    {
        [DataMember]
        public string Time { get; set; }

        [DataMember]
        public int LedStatus { get; set; }

        [DataMember]
        public string Source { get; set; }
    }
}