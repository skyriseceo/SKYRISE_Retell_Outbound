using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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

            // 2. التحقق من وجود الهيدر
            if (!request.Headers.TryGetValue(SignatureHeaderName, out var signatureHeader))
            {
                _logger.LogWarning("Webhook request missing '{HeaderName}' header.", SignatureHeaderName);
                context.Result = new BadRequestObjectResult($"Missing '{SignatureHeaderName}' header.");
                return;
            }

            var signatureHeaderText = signatureHeader.ToString();

            if (string.IsNullOrWhiteSpace(signatureHeaderText) || !signatureHeaderText.StartsWith(SignaturePrefix))
            {
                _logger.LogWarning("Invalid '{HeaderName}' format. Expected '{Prefix}'.", SignatureHeaderName, SignaturePrefix);
                context.Result = new BadRequestObjectResult($"Invalid '{SignatureHeaderName}' format.");
                return;
            }
            string requestBody;
            try
            {
                using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
                {
                    requestBody = await reader.ReadToEndAsync();
                    request.Body.Position = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read webhook request body.");
                context.Result = new StatusCodeResult(500);
                return;
            }

            byte[] computedHash;
            var secretBytes = Encoding.UTF8.GetBytes(secret);
            var bodyBytes = Encoding.UTF8.GetBytes(requestBody);

            using (var hmac = new HMACSHA256(secretBytes))
            {
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
                _logger.LogWarning("Webhook signature validation failed. Computed hash does not match header hash.");
                context.Result = new UnauthorizedObjectResult("Invalid signature."); // 401
                return;
            }

            _logger.LogInformation("Webhook signature validated successfully for {Path}.", request.Path);
            await next(); // <-- السماح للـ Controller بالعمل
        }
    }
}
