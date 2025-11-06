
using System.Text.Json.Serialization;

namespace Data.Business.Data
{

    public static class Requests
    {


        public class BookingToolRequest
        {
            [JsonPropertyName("customer_name")]
            public string CustomerName { get; set; } = string.Empty;

            [JsonPropertyName("datetime")]
            public DateTime Datetime { get; set; } = DateTime.MinValue;

            [JsonPropertyName("email")]
            public string? Email { get; set; }

            [JsonPropertyName("phone_number")]
            public string? PhoneNumber { get; set; }
        }

        // -------------------------------------------------------------------
        // (2) الـ DTOs الخاصة بالـ Webhook (ملخص نهاية المكالمة)
        // -------------------------------------------------------------------

        public class RetellWebhookEnvelope
        {
            [JsonPropertyName("event")]
            public string Event { get; set; } = string.Empty;

            [JsonPropertyName("call")]
            public RetellWebhookPayload Call { get; set; } = new RetellWebhookPayload();
        }
        public class RetellWebhookPayload
        {
            // (الـ JSON الذي أرسلته هو "Call Object" كامل، وهذا هو الـ DTO الصحيح له)

            [JsonPropertyName("call_id")]
            public string CallId { get; set; } = string.Empty;

            [JsonPropertyName("call_status")]
            public string CallStatus { get; set; } = string.Empty; // "registered", "in_progress", "completed"

            [JsonPropertyName("metadata")]
            public CallMetadata Metadata { get; set; } = new CallMetadata();

            [JsonPropertyName("call_analysis")]
            public CallAnalysis Analysis { get; set; } = new CallAnalysis();

            [JsonPropertyName("disconnection_reason")]
            public string DisconnectionReason { get; set; } = string.Empty; // "agent_hangup", "user_hangup", "call_failed"

            // (يمكنك إضافة باقي الحقول من الـ JSON مثل transcript, recording_url إذا احتجتها)
        }

        /// <summary>
        /// (قسم التحليل)
        /// </summary>
        public class CallAnalysis
        {
            [JsonPropertyName("call_summary")]
            public string CallSummary { get; set; } = string.Empty;

            [JsonPropertyName("in_voicemail")]
            public bool InVoicemail { get; set; }

            [JsonPropertyName("user_sentiment")]
            public string UserSentiment { get; set; } = string.Empty; // "Positive", "Negative", "Neutral"

            [JsonPropertyName("call_successful")]
            public bool CallSuccessful { get; set; } // <--- (هذا هو الحقل الأهم)

            // (ملاحظة: Retell لا ترسل 'call_status' داخل 'call_analysis' حسب الـ JSON)
        }

        /// <summary>
        /// (الأداة المنفردة) - (ده لو بنستخدم Post Call Analysis)
        /// </summary>
        public class ToolResult
        {
            [JsonPropertyName("tool_call_id")]
            public string ToolCallId { get; set; } = string.Empty;

            [JsonPropertyName("Name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("arguments")]
            public string Arguments { get; set; } = string.Empty;
        }

        /// <summary>
                /// (البيانات المساعدة) - (ده الكلاس الأهم اللي فيه الـ CustomerId)
                /// </summary>
        public class CallMetadata
        {
            [JsonPropertyName("our_customer_id")]
            public long OurCustomerId { get; set; }
        }

        // -------------------------------------------------------------------
        // (3) الـ DTOs الخاصة بالـ Function Node (الاتصال الفوري) - (ده الجديد)
        // -------------------------------------------------------------------

        /// <summary>
        /// (الجديد) ده الـ DTO اللي بيستقبل الـ Payload من الـ Function Node (بتاع create)
        /// </summary>
        public class RetellFunctionPayload
        {
            [JsonPropertyName("call")]
            public RetellCallObject Call { get; set; } = new RetellCallObject();

            [JsonPropertyName("args")]
            public BookingToolRequest Args { get; set; } = new BookingToolRequest();
        }

        /// <summary>
        /// (الجديد) ده الـ Call Object اللي جوه الـ Payload بتاع الـ Function
        /// </summary>
        public class RetellCallObject
        {
            [JsonPropertyName("call_id")]
            public string CallId { get; set; } = string.Empty;

            [JsonPropertyName("metadata")]
            public CallMetadata Metadata { get; set; } = new CallMetadata();
        }




        public class CreateCustomerRequest
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("phoneNumber")]
            public string phoneNumber { get; set; } = string.Empty;

            [JsonPropertyName("email")]
            public string Email { get; set; } = string.Empty;
        }
        public class BookingRequest
        {
            public string ProspectName { get; set; } = string.Empty;
            public string? ProspectEmail { get; set; }
            public string? ProspectPhone { get; set; }
            public DateTime AppointmentTime { get; set; }
            public long CallId { get; set; } = 0;
        }

        public class SendEmailRequests
        {
            public int CustomerId { get; set; }
            public string Subject { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
        }
    }
}