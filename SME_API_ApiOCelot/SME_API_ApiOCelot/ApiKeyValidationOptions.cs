namespace SME_API_ApiOCelot
{
    public class ApiKeyValidationOptions
    {
        public int CacheExpirationMinutes { get; set; }
      
        public string? ValidationUrl { get; set; }  // เพิ่ม property สำหรับ ValidationUrl
     
    }
}
