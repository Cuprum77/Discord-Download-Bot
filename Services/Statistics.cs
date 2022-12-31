using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DownloadBot.Services
{
    public class Statistics
    {
        public string YoutubeTruncate(double number)
        {
            // if the views are less than 10 000, we show the thousand digit only
            if (number < 10000 && number > 1000)
                // truncate the number, for instance, 5500 becomes 5,5
                return ((double)((int)(number / 100)) / 10) + " thousand";
            else if (number < 1000000 && number > 10000)
                return (int)(number / 1000) + " thousand";
            else if (number < 10000000 && number > 1000000)
                return ((double)((int)(number / 100000)) / 10) + " million";
            else if (number < 1000000000 && number > 10000000)
                return (int)(number / 1000000) + " million";
            else if (number < 10000000000 && number > 1000000000)
                return ((double)((int)(number / 100000000)) / 10) + " billion";
            else if (number >= 10000000000)
                return (int)(number / 1000000000) + " billion";
            // no need to truncate numbers smaller than a thousand
            else
                return number.ToString();
        }
    }
}
