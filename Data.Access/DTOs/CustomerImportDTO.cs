

namespace Data.Access.DTOs
{
    public class CustomerImportDTO
    {
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; } = string.Empty;
    }


    public class ImportResultDTO
    {
        public int TotalRows { get; set; }
        public int SuccessfullyImported { get; set; }
        public int FailedOrDuplicates { get; set; }
        public List<string> ErrorMessages { get; set; } = new List<string>();
    }
}
