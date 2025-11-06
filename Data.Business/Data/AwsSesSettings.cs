
namespace Data.Business.Data
{
    public class AwsSesSettings
    {
        public string Region { get; set; } = string.Empty;
        public string FromEmailAddress { get; set; } = string.Empty;
        public string AccessKeyId { get; set; } = string.Empty;
        public string SecretAccessKey { get; set; } = string.Empty;
        public string? ReplyToEmailAddress { get; set; }
    }
}
