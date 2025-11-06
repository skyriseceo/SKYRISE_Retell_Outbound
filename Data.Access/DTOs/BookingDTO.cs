

namespace Data.Access.DTOs
{
    public class BookingDTO
    {
        public long BookingId { get; set; }
        public string ProspectName { get; set; } = string.Empty;
        public string? ProspectEmail { get; set; }
        public string? ProspectPhone { get; set; }
        public DateTime AppointmentTime { get; set; }
        public enBookingStatus Status { get; set; } = enBookingStatus.Pending;
        public string? AgentId { get; set; }
        public string CallId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public long? CustomerId { get; set; }
    }   

    public class PaginationBookingDTO : BookingDTO
    {
        public long TotalCount { get; set; }
    }

    public enum enBookingStatus
    {
        Pending = 0,
        Confirmed = 1,
        Cancelled = 2,
    }
 
}
