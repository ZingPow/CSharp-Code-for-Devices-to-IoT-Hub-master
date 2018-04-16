using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace TempAlert
{
    [DataContract]
    class SensorEvent
    {
        [DataMember]
        public string time { get; set; }

        [DataMember]
        public string deviceid { get; set; }

        [DataMember]
        public double temperature { get; set; }

        [DataMember]
        public int ledstatus { get; set; }
    }
}
