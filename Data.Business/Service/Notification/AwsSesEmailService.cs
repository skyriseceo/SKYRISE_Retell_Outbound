using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Data.Business.Data;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Data.Business.Service.Notification
{
    public class AwsSesEmailService : IEmailSender
    {
        private readonly IAmazonSimpleEmailService _sesClient;
        private readonly ILogger<AwsSesEmailService> _logger;
        private readonly AwsSesSettings _settings;

        public AwsSesEmailService(
            IAmazonSimpleEmailService sesClient,
            IOptions<AwsSesSettings> options,
            ILogger<AwsSesEmailService> logger)
        {
            _sesClient = sesClient ?? throw new ArgumentNullException(nameof(sesClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = options.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var plainTextBody = Regex.Replace(body, "<.*?>", string.Empty);

            var request = new SendEmailRequest
            {
                Source = _settings.FromEmailAddress,
                Destination = new Destination
                {
                    ToAddresses = new List<string> { to }
                },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body
                    {
                        Html = new Content { Charset = "UTF-8", Data = body },
                        Text = new Content { Charset = "UTF-8", Data = plainTextBody }
                    }
                },
                ReplyToAddresses = string.IsNullOrEmpty(_settings.ReplyToEmailAddress)
                    ? null
                    : new List<string> { _settings.ReplyToEmailAddress }
            };

            try
            {
                _logger.LogInformation("Sending email via AWS SES → To: {To}, Subject: {Subject}", to, subject);
                var response = await _sesClient.SendEmailAsync(request);
                _logger.LogInformation("email sent successfully to {To}. Message ID: {MessageId}", to, response.MessageId);
            }
            catch (MessageRejectedException ex)
            {
                _logger.LogError(ex, "email rejected by AWS SES. To: {To}, Reason: {Reason}", to, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while sending email via AWS SES to {To}", to);
                throw;
            }
        }
    }
}
