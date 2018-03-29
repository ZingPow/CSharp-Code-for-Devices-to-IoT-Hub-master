using System.Runtime.Serialization;

namespace SimulatedDevice
{
    class LEDStatusCommand
    {
        [DataMember]
        public string time { get; set; }

        [DataMember]
        public int ledstatus { get; set; }

        [DataMember]
        public string source { get; set; }
    }
}