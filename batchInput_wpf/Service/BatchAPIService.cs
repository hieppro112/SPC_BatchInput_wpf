using batchInput_wpf.Model;
using System.CodeDom;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;

namespace batchInput_wpf.Service
{
    public class BatchAPIService
    {
        private readonly HttpClient _httpClient;
        public BatchAPIService()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("http://192.168.122.15:5044/"); // Thay đổi URL theo API của bạn
        }

        public async Task<PagedResult<BatchInputHistory>> GetAllProductAsync(int pageNumber, int pageSize)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<PagedResult<BatchInputHistory>>(
                    $"api/Batch?pageNumber={pageNumber}&pageSize={pageSize}");
                return result ?? new PagedResult<BatchInputHistory>();
            }
            catch(Exception ex)
            {
                Console.WriteLine("err: "+ex);
                return new PagedResult<BatchInputHistory>();
            }
        }

        public async Task<List<BatchInputHistory>> GetProduct_notComplate_Async()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<BatchInputHistory>>("notComplate");
                for(int i = 0; i <result.Count;i++)
                {
                    if (result[i].numRetry >=10)
                    {
                       result.RemoveAt(i);
                    }
                }
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
                var result = await _httpClient.PutAsync($"api/Batch/Update/isComplate/{id}?isComplate=true",null);
            }
            catch(Exception ex)
            {
                Console.WriteLine("err: " + ex);
            }
        }

        public async Task UpdateSTTPO(int id,bool isComplate)
        {
            try
            {
                var result = await _httpClient.PutAsync($"api/Batch/UpdatePO/{id}?isComplate={isComplate}", null);
            }
            catch (Exception ex)
            {
                Console.WriteLine("err: " + ex);
            }
        }

        public async Task update_numRetry(int id)
        {
            try
            {
                var result = await _httpClient.PutAsync($"api/Batch/Update/Batch_numTry/{id}", null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("lỗi : " + ex);
                //return new List<ItemGetCount>();
            }
        }

        public async Task update_des(int id,string descrip)
        {
            try
            {
                string encodeDesc = Uri.EscapeDataString(descrip);
                var result = await _httpClient.PutAsync($"api/Batch/Update/Batch_descriptions/{id}?description={encodeDesc}",null);
                //return result ?? new List<ItemGetCount>();
            }
            catch (Exception ex)
            {
                MessageBox.Show("lỗi : " + ex);
                //return new List<ItemGetCount>();
            }
        }

        public async Task update_runTime(int id)
        {
            try
            {
                var result = await _httpClient.PutAsync($"api/Batch/Update/RunTime/{id}", null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("lỗi : " + ex);
                //return new List<ItemGetCount>();
            }
        }

        public async Task update_desPO(int id, string descrip)
        {
            try
            {
                string encodeDesc = Uri.EscapeDataString(descrip);
                var result = await _httpClient.PutAsync($"api/Batch/UpdatePO/Description/{id}?desPo={encodeDesc}", null);
                //return result ?? new List<ItemGetCount>();
            }
            catch (Exception ex)
            {
                MessageBox.Show("lỗi : " + ex);
                //return new List<ItemGetCount>();
            }
        }
    }
}
