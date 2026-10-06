using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace batchInput_wpf.Model
{
    public class ItemSaveConfig
    {
        public string? pathSaveImg { get; set; } = string.Empty;
        public string? pathLogErr { get; set; } = string.Empty;
        public int PollDelaySeconds { get; set; }
        public bool HeadlessMode { get; set; } = true;
        public int MaxThreads { get; set; } 
    }
}
