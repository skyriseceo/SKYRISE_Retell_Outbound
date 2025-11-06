namespace Data.Access.DTOs
{
    public enum enStatus
    {
        New = 0,
        Calling = 1,
        Booked = 2,
        Failed = 3,
        Contacted = 4,
        NoAnswer = 5
    }

    public class CustomerDTO
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; } 
        public enStatus Status { get; set; } = enStatus.New;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PaginationCustomerDTO : CustomerDTO
    {
        public int TotalCount { get; set; } = 0;
    }


}
