using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebimSDK
{
    class DateTimeHelper
    {
        public static DateTime DateTimeFromTimestamp(double timestamp)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return origin.AddSeconds(timestamp).ToLocalTime();
        }

        public static double TimestampFromDateTime(DateTime dateTime)
        {
            DateTime sTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (dateTime - sTime).TotalSeconds;
        }

        public static double Timestamp()
        {
            return TimestampFromDateTime(DateTime.UtcNow);
        }
    }
}
