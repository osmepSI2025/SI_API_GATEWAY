using Microsoft.AspNetCore.Http;
using Microsoft.VisualBasic;
using SME_API_ApiOCelot.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace SME_API_ApiOCelot.Services
{
    public class CallAPIService : ICallAPIService
    {
        private readonly HttpClient _httpClient;

         private readonly string Api_ErrorLog;
        private readonly string Api_SysCode;


        public CallAPIService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            Api_ErrorLog = configuration["Information:Api_ErrorLog"] ?? throw new ArgumentNullException("Api_ErrorLog is missing in appsettings.json");
            Api_SysCode = configuration["Information:Api_SysCode"] ?? throw new ArgumentNullException("Api_SysCode is missing in appsettings.json");


        }

      public async Task RecErrorLogApiAsync(TErrorApiLogModels eModels,string strApikey)
        {
    
            // ✅ เลือก URL ตาม _FlagDev
            string?  apiUrl = Api_ErrorLog;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);

                // ✅ ใส่ API Key ถ้ามี
                if (!string.IsNullOrEmpty(strApikey))
                    request.Headers.Add("X-Api-Key", strApikey);

               
                // ✅ แปลง SendData เป็น JSON และแนบไปกับ Body ของ Request
                var jsonData = JsonSerializer.Serialize(eModels);
                request.Content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                // ✅ Call API และรอผลลัพธ์
                using var response = await _httpClient.SendAsync(request);
                string responseData = await response.Content.ReadAsStringAsync();

            }
            catch (Exception ex)
            {
                
            }
        }

    }
}
