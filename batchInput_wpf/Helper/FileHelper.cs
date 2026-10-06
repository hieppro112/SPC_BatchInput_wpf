using batchInput_wpf.Model;
using Google.Api.Ads.Common.Lib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace batchInput_wpf.Helper
{
    public class FileHelper
    {
        public ItemSaveConfig ReadJson(string path = @"C:\BatchInput_config\config_batchInput.json")
        {
            if (File.Exists(path))
            {
                string jsonW =File.ReadAllText(path);
                ItemSaveConfig config = JsonSerializer.Deserialize<ItemSaveConfig>(jsonW);
                return new ItemSaveConfig
                {
                    pathSaveImg = config.pathSaveImg,
                    pathLogErr = config.pathLogErr,
                    PollDelaySeconds = config.PollDelaySeconds,
                    HeadlessMode = config.HeadlessMode,
                    MaxThreads = config.MaxThreads
                };
            }
            else
            {
                ItemSaveConfig item = new ItemSaveConfig();
                string json = JsonSerializer.Serialize(item);
                Directory.CreateDirectory(@"C:/BatchInput_config/");
                File.WriteAllText(@"C:/BatchInput_config/config_batchInput.json", json);
                return item;
            }
        }
        public void WriteJson(ItemSaveConfig item)
        {
            //xuat json 
            string json = JsonSerializer.Serialize(item);
            Directory.CreateDirectory(@"C:/BatchInput_config/");
            File.WriteAllText(@"C:/BatchInput_config/config_batchInput.json", json);
        }
    }
}
