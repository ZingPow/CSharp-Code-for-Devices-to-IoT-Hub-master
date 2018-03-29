using System.Runtime.Serialization;

namespace SimulatedDevice
{
    [DataContract]
    class TelemetryData
    {
        [DataMember]
        internal string Time;

        [DataMember]
        internal string DeviceId;

        [DataMember]
        internal double Temperature;

        [DataMember]
        internal int LEDStatus;
    }
}