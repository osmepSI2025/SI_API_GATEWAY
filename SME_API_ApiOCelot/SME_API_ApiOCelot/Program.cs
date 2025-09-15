


//////24052025
//using Ocelot.DependencyInjection;
//using Ocelot.Middleware;
//using MMLib.SwaggerForOcelot;
//using Microsoft.OpenApi.Models;
//using Microsoft.Extensions.Caching.Memory;
//using Microsoft.Extensions.Options;
//using SME_API_ApiOCelot;

//var builder = WebApplication.CreateBuilder(args);

//// 👉 โหลดไฟล์ ocelot.json
//builder.Configuration.AddJsonFile("ApiGateway/ocelot.json", optional: false, reloadOnChange: true);

//// 👉 Register Service
//builder.Services.AddControllers();

//// 👉 เพิ่ม HttpClient
//builder.Services.AddHttpClient();

//// 👉 เพิ่ม MemoryCache
//builder.Services.AddMemoryCache();

//// 👉 เพิ่ม Options สำหรับ API Key Validation (อ่านจาก appsettings.json)
//builder.Services.Configure<ApiKeyValidationOptions>(builder.Configuration.GetSection("ApiKeyValidation"));
//// Load all route files in the ApiGateway folder
//foreach (var file in Directory.GetFiles("Routes", "*.json"))
//{
//    if (!file.EndsWith("ocelot.json", StringComparison.OrdinalIgnoreCase))
//    {
//        builder.Configuration.AddJsonFile(file, optional: false, reloadOnChange: true);
//    }
//}
//// 👉 เพิ่ม Ocelot
//builder.Services.AddOcelot(builder.Configuration);

//// 👉 Swagger สำหรับ Ocelot
//builder.Services.AddSwaggerForOcelot(builder.Configuration);
//builder.Services.AddSwaggerGen(c =>
//{
//    c.SwaggerDoc("v1", new OpenApiInfo
//    {
//        Title = "API Gateway",
//        Version = "v1"
//    });
//});

//var app = builder.Build();

//// 👉 ใช้ Swagger UI สำหรับ Ocelot
//app.UseSwaggerForOcelotUI(options =>
//{
//    options.PathToSwaggerGenerator = "/swagger/docs";
//});

//// 👉 เพิ่ม Middleware เช็ค API Key ก่อนถึง Ocelot
//app.UseMiddleware<ApiKeyValidationMiddleware>();

//// 👉 ใช้ Ocelot Middleware
//await app.UseOcelot();

//app.Run();


using Microsoft.OpenApi.Models;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using SME_API_ApiOCelot;
using System.Text.Json;
using Serilog;
using Serilog.Events;
using SME_API_ApiOCelot.Services;
//using Serilog.Extensions.Hosting; // Add this namespace for UseSerilog extension

try 
{


    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ListenAnyIP(10100); // เปิดพอร์ต 10100
    });
    // Use the Serilog extension method for the Host
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });
    // Load global Ocelot config (GlobalConfiguration, SwaggerEndPoints, etc.)
    builder.Configuration.AddJsonFile("ApiGateway/ocelot.json", optional: false, reloadOnChange: true);

    // Merge all route files in the Routes folder
    var routeFiles = Directory.GetFiles("Routes", "*.json");
    var allRoutes = new List<object>();

    foreach (var file in routeFiles)
    {
        using var stream = File.OpenRead(file);
        using var doc = JsonDocument.Parse(stream);
        if (doc.RootElement.TryGetProperty("Routes", out var routesElement))
        {
            foreach (var route in routesElement.EnumerateArray())
            {
                allRoutes.Add(JsonSerializer.Deserialize<object>(route.GetRawText()));
            }
        }
    }

    // Write merged routes to a temp file
    var mergedRoutes = new { Routes = allRoutes };
    var mergedRoutesPath = Path.Combine(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"), "ocelot.merged.routes.json");

    File.WriteAllText(mergedRoutesPath, JsonSerializer.Serialize(mergedRoutes));

    // Add merged routes to configuration
    builder.Configuration.AddJsonFile(mergedRoutesPath, optional: false, reloadOnChange: true);

    // Register services
    builder.Services.AddControllers();
    builder.Services.AddHttpClient();
    builder.Services.AddMemoryCache();
    builder.Services.Configure<ApiKeyValidationOptions>(builder.Configuration.GetSection("ApiKeyValidation"));
    builder.Services.AddOcelot(builder.Configuration);
    // Add this in the section where you configure services
    builder.Services.AddScoped<ICallAPIService, CallAPIService>();
    builder.Services.AddSwaggerForOcelot(builder.Configuration);
   
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "API Gateway",
            Version = "v1"
        });

        // Add X-Api-Key header support
        c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Description = "API Key needed to access the endpoints. X-Api-Key: {key}",
            In = ParameterLocation.Header,
            Name = "X-Api-Key",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "ApiKeyScheme"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            new List<string>()
        }
    });
    });

    var app = builder.Build();

    // Use Swagger UI for Ocelot
    app.UseSwaggerForOcelotUI(options =>
    {
        options.PathToSwaggerGenerator = "/swagger/docs";
    });

    // Use API Key validation middleware
    app.UseMiddleware<ApiKeyValidationMiddleware>();

    // Use Ocelot middleware
    await app.UseOcelot();

    app.Run();

}
catch (Exception ex)
{
    Console.WriteLine($"Serilog initialization failed: {ex.Message}");
}

