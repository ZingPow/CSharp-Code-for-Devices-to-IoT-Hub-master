using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RPDevice.Models
{
    public class ConfigData
    {
        public DateTime ReadingDateTime { get; set; }

        public double Temperature { get; set; }

        public int LED { get; set; }
    }
}
