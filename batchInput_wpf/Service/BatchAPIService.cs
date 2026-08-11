using batchInput_wpf.Model;
using System.CodeDom;
using System.Net.Http;
using System.Net.Http.Json;

namespace batchInput_wpf.Service
{
    public class BatchAPIService
    {
        private readonly HttpClient _httpClient;
        public BatchAPIService()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("http://192.168.122.15:5044/"); // Thay đổi URL theo API của bạn
            //_httpClient.BaseAddress = new Uri("http://localhost:5044/"); // Thay đổi URL theo API của bạn
        }

        public async Task<List<BatchInputHistory>> GetAllProductAsync()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<BatchInputHistory>>("api/Batch");
                return result ?? new List<BatchInputHistory>();
            }
            catch(Exception ex)
            {
                Console.WriteLine("err: "+ex);
                return new List<BatchInputHistory>();
            }
        }

        public async Task<List<BatchInputHistory>> GetProduct_notComplate_Async()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<BatchInputHistory>>("notComplate");
                return result ?? new List<BatchInputHistory>();
            }
            catch (Exception ex)
            {
                Console.WriteLine("err: " + ex);
                return new List<BatchInputHistory>();
            }
        }

        public async Task UpdateItem(int id)
        {
            try
            {
                var result = await _httpClient.PutAsync($"api/Batch/Update/{id}?isComplate=true",null);
            }
            catch(Exception ex)
            {
                Console.WriteLine("err: " + ex);
            }
        }

        public async Task<List<ItemGetCount>> GetCount()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<ItemGetCount>>("api/Batch/GetCount");
                return result ?? new List<ItemGetCount>();
            }
            catch (Exception ex)
            {
                Console.WriteLine("err: " + ex);
                return new List<ItemGetCount>();
            }
        }

    }
}
