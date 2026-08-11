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
                    pathLogErr = config.pathLogErr
                };
            }
            else
            {
                ItemSaveConfig item = new ItemSaveConfig();
                string json = JsonSerializer.Serialize(item);
                Directory.CreateDirectory(@"C:/BatchInput_config/");
                File.WriteAllText(@"C:/BatchInput_config/config_batchInput.json", json);
                return null;
            }
            return null;
        }
        public void WriteJson(ItemSaveConfig item)
        {
            //xuat json 
            string json = JsonSerializer.Serialize(item);
            Directory.CreateDirectory(@"C:/BatchInput_config/");
            File.WriteAllText(@"C:/BatchInput_config/config_batchInput.json", json);
        }
        private string GetDataConfig(String s_search)
        {
            //string path = @$"D:\jobs\IT\pcx\batchInput_wpf\Image_err\{itemBatch.ID}";
            string folder_path = @"C:\BatchInput_config\config.txt";
            string configPath = Path.Combine(folder_path, "config.txt");
            Directory.CreateDirectory(configPath);
            if (!File.Exists(configPath))
            {
                File.WriteAllText(configPath, "");
                return "";
            }

            try
            {
                // Kiểm tra file có tồn tại không
                if (!File.Exists(configPath))
                {
                    MessageBox.Show($"Không tìm thấy file config tại: {configPath}");
                    return "";
                }

                // Đọc tất cả các dòng trong file
                string[] lines = File.ReadAllLines(configPath);

                // Tìm dòng bắt đầu bằng "Name_Servo:"
                foreach (string line in lines)
                {
                    if (line.StartsWith($"{s_search}:"))
                    {

                        string port = line.Split(':')[1];
                        return port;
                    }
                }
                // Không tìm thấy dòng Name_Servo
                MessageBox.Show($"Không tìm thấy cấu hình {s_search} trong file config");
                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đọc file config: {ex.Message}");
                return null;
            }
        }
    }
}
