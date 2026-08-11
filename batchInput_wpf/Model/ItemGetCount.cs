using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace batchInput_wpf.Model
{
    public class ItemGetCount
    {
        [JsonPropertyName("runDate")]
        public DateTime date { get; set; }
        [JsonPropertyName("count")]
        public int count { get; set; }
    }
}
