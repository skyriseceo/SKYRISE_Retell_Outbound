using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Cryptography; 
using System.Text;

namespace OSV.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ApiKeyAuthAttribute : Attribute, IAsyncActionFilter
    {
        private const string API_KEY_HEADER_NAME = "X-API-Key";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
           
            if (!context.HttpContext.Request.Headers.TryGetValue(API_KEY_HEADER_NAME, out var extractedApiKey))
            {
                context.Result = new ContentResult()
                {
                    StatusCode = 401,
                    Content = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        success = false,
                        message = "API Key is missing. Please provide a valid API Key in the 'X-API-Key' header."
                    }),
                    ContentType = "application/json"
                };
                return;
            }

            // 2. جلب المفتاح الصحيح من الإعدادات
            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var validApiKey = configuration["ApiSettings:RetellWebhookApiKey"];

            // 3. التحقق من أن المفتاح مُعد في السيرفر
            if (string.IsNullOrWhiteSpace(validApiKey))
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ApiKeyAuthAttribute>>();
                logger.LogError("API Key is not configured in appsettings.json. Please set 'ApiSettings:RetellWebhookApiKey'.");

                context.Result = new ContentResult()
                {
                    StatusCode = 500,
                    Content = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        success = false,
                        message = "Server configuration error. Please contact the administrator."
                    }),
                    ContentType = "application/json"
                };
                return;
            }

            var validApiKeyBytes = Encoding.UTF8.GetBytes(validApiKey);
            var extractedApiKeyBytes = Encoding.UTF8.GetBytes(extractedApiKey.ToString());

            if (!CryptographicOperations.FixedTimeEquals(validApiKeyBytes, extractedApiKeyBytes))
            {
                context.Result = new ContentResult()
                {
                    StatusCode = 401,
                    Content = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        success = false,
                        message = "Invalid API Key. Access denied."
                    }),
                    ContentType = "application/json"
                };
                return;
            }
            await next();
        }
    }
}