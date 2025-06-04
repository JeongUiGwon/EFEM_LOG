using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RORZE_LOG.Models
{
    public class MCLogModel
    {
        public string Time { get; set; }
        public string Type { get; set; }
        public string Command { get; set; }
        public string CommandGroup { get; set; }
        public double ElapsedTime { get; set; }
        public string Data { get; set; }
    }
}
