using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace TempAlert
{
    [DataContract]
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
