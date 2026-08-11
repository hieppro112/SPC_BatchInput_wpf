using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace batchInput_wpf.Model
{
    public class LogItem
    {
        public string Message { get; set; }
    public LogStatus Status { get; set; }
    public string Time =>
   DateTime.Now.ToString("HH:mm:ss");
    
    }
}
