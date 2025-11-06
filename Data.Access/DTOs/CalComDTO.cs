using System.Text.Json.Serialization;

namespace Data.Access.DTOs
{

    public class ExternalBookingViewModel
    {
        public string Customer { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime BookingDate { get; set; } = DateTime.MinValue;
        public DateTime CreatedAt { get; set; } = DateTime.MinValue;
    }

    public class CalApiResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public List<CalBooking> Data { get; set; } = new List<CalBooking>();

        [JsonPropertyName("pagination")]
        public CalPagination Pagination { get; set; } = new CalPagination();
    }

    public class CalBooking
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("uid")]
        public string Uid { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("start")]
        public DateTime Start { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("attendees")]
        public List<CalAttendee> Attendees { get; set; } = new List<CalAttendee>();
    }

    public class CalAttendee
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("phoneNumber")]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class CalPagination
    {
        [JsonPropertyName("totalItems")]
        public int TotalItems { get; set; }

        [JsonPropertyName("totalPages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("currentPage")]
        public int CurrentPage { get; set; }
    }

    public class CalComWebhookPayload
    {
        [JsonPropertyName("triggerEvent")]
        public string TriggerEvent { get; set; } = string.Empty;

        [JsonPropertyName("payload")]
        public WebhookPayloadData Payload { get; set; } = new WebhookPayloadData();
    }

    public class WebhookPayloadData
    {
        [JsonPropertyName("uid")]
        public string Uid { get; set; } = string.Empty;

        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; } // (موعد الاجتماع)

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("attendees")]
        public List<WebhookAttendee> Attendees { get; set; } = new List<WebhookAttendee>();

        [JsonPropertyName("oldStartTime")]
        public DateTime? OldStartTime { get; set; }
    }

    public class WebhookAttendee
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }= string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        // (ملاحظة: Cal.com لا يرسل رقم الهاتف في الـ Webhook للأسف)
        // (سنحتاج لتعديل الدالة في الداتا بيز أو إرسال قيمة null)
        // (تحديث: الدالة fn_add_booking لديك تقبل nulls، هذا ممتاز)
    }
}
