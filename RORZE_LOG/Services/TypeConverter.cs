using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RORZE_LOG.Services
{
    public class TypeConverter
    {
        public DateTime stringToDateTime(string time)
        {
            if (string.IsNullOrEmpty(time)) return DateTime.MinValue;
            try
            {
                // Assuming the format is "MM-dd-yy HH:mm:ss:fff"
                return DateTime.ParseExact(time, "yy-MM-dd HH:mm:ss:fff", null);
            }
            catch
            {
                return DateTime.MinValue; // Return a default value if parsing fails
            }
        }
    }
}
