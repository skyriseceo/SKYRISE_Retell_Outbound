using Data.Access.DTOs;

namespace Data.Access.Customer
{
    public interface ICustomerRepository
    {
        Task<long?> AddCustomerAsync(string name, string phoneNumber, string? email, CancellationToken cancellationToken = default);
        Task<CustomerDTO?> GetCustomerByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default);
        Task<PagedList<PaginationCustomerDTO>> GetAllCustomersAsync(PaginationParameters parameters, CancellationToken cancellationToken = default);
        Task<bool> DeleteCustomerAsync(long customerId, CancellationToken cancellationToken = default);        
        Task<bool> UpdateCustomerAsync(CustomerDTO customer, CancellationToken cancellationToken = default);        
        Task<CustomerDTO?> GetCustomerByIdAsync(long customerId, CancellationToken cancellationToken = default);
        Task<IEnumerable<CustomerDTO>> GetCustomersByStatusAsync(enStatus status, CancellationToken cancellationToken = default);
        Task<bool> UpdateCustomerStatusAsync(long customerId, enStatus Newstatus, enStatus oldStatus, CancellationToken cancellationToken = default);
        Task<CustomerStatisticsDTO?> GetCustomerStatisticsAsync(CancellationToken cancellationToken = default);

        Task<int?> AddCustomersBulkAsync(IEnumerable<CustomerImportDTO> customers, CancellationToken cancellationToken = default);
    }
}