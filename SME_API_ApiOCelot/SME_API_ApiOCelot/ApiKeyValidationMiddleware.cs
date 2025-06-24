namespace SME_API_ApiOCelot
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using SME_API_ApiOCelot.Models;
    using SME_API_ApiOCelot.Services;
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class ApiKeyValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiKeyValidationMiddleware> _logger;
        private readonly IHttpClientFactory _httpClientFactory;  // เปลี่ยนชื่อเป็น IHttpClientFactory
        private readonly IMemoryCache _cache;
        private readonly IOptions<ApiKeyValidationOptions> _options;
    
        private readonly IServiceScopeFactory _scopeFactory;


        public ApiKeyValidationMiddleware(
            RequestDelegate next,
            ILogger<ApiKeyValidationMiddleware> logger,
            IHttpClientFactory httpClientFactory,  // รับ IHttpClientFactory
            IMemoryCache cache,
            IOptions<ApiKeyValidationOptions> options
          
, IServiceScopeFactory scopeFactory

            ) // Inject ICallAPIService
        {
            _next = next;
            _logger = logger;
            _httpClientFactory = httpClientFactory;  // เก็บค่า IHttpClientFactory
            _cache = cache;
            _options = options;
          
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

        }



        //public async Task Invoke(HttpContext context)
        //{
        //    var apiKey = context.Request.Headers["X-Api-Key"].ToString();

        //    if (string.IsNullOrEmpty(apiKey))
        //    {
        //        context.Response.StatusCode = 401;
        //        await context.Response.WriteAsync("API Key is missing.");
        //        return;
        //    }

        //    var cacheKey = $"ApiKeyValidation_{apiKey}";
        //    bool isValid = false;

        //    // Check if cache already contains the validation result
        //    if (!_cache.TryGetValue(cacheKey, out isValid))
        //    {
        //        // If not in cache, validate the API key externally
        //        isValid = await ValidateApiKey(apiKey);

        //        // Set cache expiration time
        //        var cacheExpiration = _options.Value.CacheExpirationMinutes > 0
        //            ? TimeSpan.FromMinutes(_options.Value.CacheExpirationMinutes)
        //            : TimeSpan.FromMinutes(5);

        //        // Store the result in cache
        //        _cache.Set(cacheKey, isValid, cacheExpiration);
        //    }

        //    if (!isValid)
        //    {
        //        context.Response.StatusCode = 401;
        //        await context.Response.WriteAsync("Invalid API Key.");
        //        return;
        //    }

        //    await _next(context);
        //}

        public async Task Invoke(HttpContext context)
        {
           

            var apiKey = context.Request.Headers["X-Api-Key"].ToString();

            // 1. ตรวจสอบ API Key ว่ามีหรือไม่
            if (string.IsNullOrEmpty(apiKey))
            {
               

                if (!context.Response.HasStarted)
                {
                    var errorResponse = new ErrorResponseModels
                    {
                        responseCode = "400",
                        responseMsg = "API Key is missing."
                    };

                    context.Response.ContentType = "application/json";
                    var json = JsonSerializer.Serialize(errorResponse);
                    await context.Response.WriteAsync(json);
                }
                return;
            }

            // 2. ตรวจสอบความถูกต้องของ API Key (cache + external)
            //var validationUrl = context.Request.Path;
            //var cacheKey = $"ApiKeyValidation_{apiKey}";
            //bool isValid = false;
            //if (!_cache.TryGetValue(cacheKey, out isValid))
            //{
            //    isValid = await ValidateApiKey(apiKey, validationUrl);
            //    var cacheExpiration = _options.Value.CacheExpirationMinutes > 0
            //        ? TimeSpan.FromMinutes(_options.Value.CacheExpirationMinutes)
            //        : TimeSpan.FromMinutes(5);
            //    _cache.Set(cacheKey, isValid, cacheExpiration);
            //}

            var validationUrl = context.Request.Path;

            // เรียกตรวจสอบ API Key กับ path ทุกครั้ง ไม่ใช้ cache
            bool isValid = await ValidateApiKey(apiKey, validationUrl);
            if (!isValid)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
             
                var errorResponse = new ErrorResponseModels
                {
                    responseCode = "403",
                    responseMsg = "API Key is missing."
                };

                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(errorResponse);
                await context.Response.WriteAsync(json);
                return;
            }

            // 3. เรียก middleware ถัดไป
            await _next(context);

            // 4. ตรวจสอบ response ถ้าไม่ใช่ 200 ให้ log error
            if (context.Response.StatusCode != StatusCodes.Status200OK)
            {
                var statusCode = context.Response.StatusCode;
                var errorLog = new TErrorApiLogModels
                {
                    HttpCode = statusCode.ToString(),
                    ErrorDate = DateTime.Now,
                    Path = context.Request.Path,
                    HttpMethod = context.Request.Method,
                    CreatedBy = context.User.Identity?.Name ?? "system",
                    Source = "SYS-GATEWAY",
                    TargetSite = "InvokeAsync",
                    SystemCode = "SYS-GATEWAY"
                };

                switch (statusCode)
                {
                    case StatusCodes.Status400BadRequest:
                        errorLog.HttpCode = "400";
                        errorLog.Message = "Bad request";
                        errorLog.StackTrace = "The request could not be understood or was missing required parameters.";
                        break;
                    case StatusCodes.Status401Unauthorized:
                        errorLog.HttpCode = "401";
                        errorLog.Message = "Unauthorized access";
                        errorLog.StackTrace = "The request requires user authentication.";
                        break;
                    case StatusCodes.Status403Forbidden:
                        errorLog.HttpCode = "403";
                        errorLog.Message = "Forbidden";
                        errorLog.StackTrace = "The server understood the request, but refuses to authorize it.";
                        break;
                    case StatusCodes.Status404NotFound:
                        errorLog.HttpCode = "404";
                        errorLog.Message = "Resource not found";
                        errorLog.StackTrace = "The requested resource could not be found.";
                        break;
                    case StatusCodes.Status405MethodNotAllowed:
                        errorLog.HttpCode = "405";
                        errorLog.Message = "Method not allowed";
                        errorLog.StackTrace = "The HTTP method is not allowed for the requested resource.";
                        break;
                    case StatusCodes.Status500InternalServerError:
                        errorLog.HttpCode = "500";
                        errorLog.Message = "Internal server error";
                        errorLog.StackTrace = "An unexpected error occurred on the server.";
                        break;
                    default:
                        return;
                }
                using var scope = _scopeFactory.CreateScope();
                var callApiService = scope.ServiceProvider.GetRequiredService<ICallAPIService>();
                await callApiService.RecErrorLogApiAsync(errorLog, apiKey);

                if (!context.Response.HasStarted)
                {
                    var errorResponse = new ErrorResponseModels
                    {
                        responseCode = statusCode.ToString(),
                        responseMsg = errorLog.Message
                    };

                    context.Response.ContentType = "application/json";
                    var json = JsonSerializer.Serialize(errorResponse);
                    await context.Response.WriteAsync(json);
                }
            }
        }

        private async Task<bool> ValidateApiKey(string apiKey, string path)
        {
            try
            {
                var validationUrl = _options.Value.ValidationUrl;
                var client = _httpClientFactory.CreateClient();

                // Ensure path is URL-encoded and does not start with a slash
                byte[] byteData = System.Text.Encoding.UTF8.GetBytes(path);
                string xUrl = Convert.ToBase64String(byteData);
            

                // Build the full URL with apiKey and path as parameters
                var url = $"{validationUrl}{apiKey}/{xUrl}";

                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    _logger.LogWarning($"API Key validation failed. Status code: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during API Key validation: {ex.Message}");
                return false;
            }
        }
    }


}
