using Data.Access.DTOs;
using Data.Business.Data;
using Microsoft.AspNetCore.Http;


namespace Data.Business.Customer
{
    public interface ICustomerService
    {
        Task<long?> CreateCustomerAsync(string name, string phoneNumber, string? email, CancellationToken cancellationToken = default);
        Task<CustomerDTO?> GetCustomerByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default);
        Task<PagedList<PaginationCustomerDTO>> GetAllCustomersAsync(PaginationParameters parameters, CancellationToken cancellationToken = default);
        Task<bool> DeleteCustomerAsync(long customerId, CancellationToken cancellationToken = default);
        Task<bool> UpdateCustomerAsync(CustomerDTO customer, CancellationToken cancellationToken = default);
        Task<CustomerDTO?> GetCustomerByIdAsync(long customerId, CancellationToken cancellationToken = default);
        Task<IEnumerable<CustomerDTO>> GetCustomersByStatusAsync(enStatus status, CancellationToken cancellationToken = default);
        Task<CustomerStatisticsDTO?> GetCustomerStatisticsAsync(CancellationToken cancellationToken = default);
        Task<bool> StartCallAsync(long customerId, CancellationToken cancellationToken = default);
        Task<bool> HandleWebhookCallbackAsync(Requests.RetellWebhookEnvelope envelope, CancellationToken cancellationToken = default);
        Task<bool> SendEmailToCustomerAsync(long customerId, string subject, string body, CancellationToken cancellationToken = default);
        Task<bool> UpdateAndBroadcastStatus(CustomerDTO customer, enStatus newStatus, enStatus oldStatus, CancellationToken cancellationToken);
        Task<ImportResultDTO> ImportCustomersAsync(IFormFile file, CancellationToken cancellationToken = default);

        Task<bool> UpdateCustomerStatusAsync(long customerId, enStatus newStatus, enStatus oldStatus, CancellationToken cancellationToken = default);
    }
}
