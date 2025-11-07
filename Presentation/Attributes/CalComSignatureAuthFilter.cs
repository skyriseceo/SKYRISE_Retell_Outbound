using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace OSV.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class CalComSignatureAuthFilter : Attribute, IAsyncActionFilter
    {
        private readonly IConfiguration _config;
        private readonly ILogger<CalComSignatureAuthFilter> _logger;

        private const string SignatureHeaderName = "X-Cal-Signature-256";
        private const string SignaturePrefix = "sha256=";

        public CalComSignatureAuthFilter(IConfiguration config, ILogger<CalComSignatureAuthFilter> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var secret = _config["CalCom:WebhookSecret"];
            if (string.IsNullOrEmpty(secret))
            {
                _logger.LogError("Webhook secret 'CalCom:WebhookSecret' is not configured.");
                context.Result = new StatusCodeResult(500);
                return;
            }

            var request = context.HttpContext.Request;

            if (!request.Headers.TryGetValue(SignatureHeaderName, out var signatureHeader))
            {
                _logger.LogWarning("Webhook request missing '{HeaderName}' header.", SignatureHeaderName);
                context.Result = new BadRequestObjectResult($"Missing '{SignatureHeaderName}' header.");
                return;
            }

            var signatureHeaderText = signatureHeader.ToString();
            _logger.LogWarning("Received '{HeaderName}' header with raw value: [{Value}]",
        SignatureHeaderName, signatureHeaderText);

            if (string.IsNullOrWhiteSpace(signatureHeaderText) || !signatureHeaderText.StartsWith(SignaturePrefix))
            {
                _logger.LogWarning("Invalid '{HeaderName}' format. Expected '{Prefix}'.", SignatureHeaderName, SignaturePrefix);
                context.Result = new BadRequestObjectResult($"Invalid Header format.{{{SignaturePrefix}}}");
                return;
            }

            // ---------------- [بداية التعديل الجذري] ----------------
            // لن نستخدم 'StreamReader' أو 'string'

            byte[] bodyBytes;
            try
            {
                // 1. إرجاع المؤشر (ما زلنا نحتاج هذا بسبب [FromBody])
                request.Body.Position = 0;

                // 2. قراءة الـ Stream مباشرة إلى MemoryStream ثم إلى byte[]
                // هذه الطريقة تضمن أننا نقرأ الـ raw bytes كما هي
                using (var ms = new MemoryStream())
                {
                    await request.Body.CopyToAsync(ms);
                    bodyBytes = ms.ToArray();
                }

                // 3. إرجاع المؤشر مرة أخرى (Best practice)
                request.Body.Position = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read webhook request body bytes.");
                context.Result = new StatusCodeResult(500);
                return;
            }

            // ---------------- [نهاية التعديل الجذري] ----------------


            byte[] computedHash;
            var secretBytes = Encoding.UTF8.GetBytes(secret);

            using (var hmac = new HMACSHA256(secretBytes))
            {
                // 4. حساب الـ Hash على الـ raw bytes مباشرة
                computedHash = hmac.ComputeHash(bodyBytes);
            }

            byte[] headerHashBytes;
            try
            {
                var hexSignature = signatureHeaderText.Substring(SignaturePrefix.Length);
                headerHashBytes = Convert.FromHexString(hexSignature);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid hex signature in '{HeaderName}'.", SignatureHeaderName);
                context.Result = new BadRequestObjectResult("Invalid signature hex format.");
                return;
            }

            if (!CryptographicOperations.FixedTimeEquals(computedHash, headerHashBytes))
            {
                // إذا استمر الفشل هنا، فالـ Secret Key في Postman مختلف 100% عن السيرفر
                _logger.LogWarning("Webhook signature validation failed. Computed hash does not match header hash.");
                context.Result = new UnauthorizedObjectResult("Invalid signature.");
                return;
            }

            _logger.LogInformation("Webhook signature validated successfully for {Path}.", request.Path);
            await next();
        }
    }
}
