using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Cryptography;
using System.Text;


namespace OSV.Attributes // (تأكد أن الـ Namespace صحيح)
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RetellSignatureAuthAttribute : Attribute, IAsyncActionFilter
    {
        private const string SIGNATURE_HEADER_NAME = "x-retell-signature";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RetellSignatureAuthAttribute>>();

            // 1. جلب التوقيع من الـ Header
            if (!context.HttpContext.Request.Headers.TryGetValue(SIGNATURE_HEADER_NAME, out var extractedSignature))
            {
                logger.LogWarning("Auth Failure: Missing '{Header}' header.", SIGNATURE_HEADER_NAME);
                context.Result = new UnauthorizedObjectResult(new { success = false, message = $"Missing '{SIGNATURE_HEADER_NAME}' header." });
                return;
            }

            // 2. جلب المفتاح السري من الإعدادات
            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var retellApiKey = configuration["Retell:ApiKey"]; // (يجب أن يكون هذا هو المفتاح السري من Retell Dashboard)

            if (string.IsNullOrEmpty(retellApiKey))
            {
                logger.LogError("Server Configuration Error: 'Retell:ApiKey' is not set in appsettings.json.");
                context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
                return;
            }

            string requestBody;
            try
            {
                // 3. (هام جداً) قراءة الـ Raw Body
                // نحتاج تفعيل .EnableBuffering() في Program.cs
                context.HttpContext.Request.Body.Position = 0;
                using (var reader = new StreamReader(context.HttpContext.Request.Body, Encoding.UTF8, leaveOpen: true))
                {
                    requestBody = await reader.ReadToEndAsync();
                }
                // (هام جداً) إرجاع الـ Stream للبداية ليتمكن الـ Controller من قراءته
                context.HttpContext.Request.Body.Position = 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to read request body.");
                context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
                return;
            }

            // 4. التحقق من التوقيع (HMAC-SHA256)
            try
            {
                var keyBytes = Encoding.UTF8.GetBytes(retellApiKey);
                using (var hmac = new HMACSHA256(keyBytes))
                {
                    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestBody));
                    // التوقيع الذي ترسله Retell هو Base64
                    var computedSignature = Convert.ToBase64String(hash);

                    if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(computedSignature),
                        Encoding.UTF8.GetBytes(extractedSignature.ToString())))
                    {
                        logger.LogWarning("Auth Failure: Invalid signature. Computed: {Computed}, Received: {Received}", computedSignature, extractedSignature);
                        context.Result = new UnauthorizedObjectResult(new { success = false, message = "Invalid signature." });
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during signature validation.");
                context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
                return;
            }

            // 5. التوقيع سليم، اسمح للطلب بالمرور
            logger.LogInformation("Retell signature validated successfully.");
            await next();
        }
    }
}