using Dapper;
using Data.Access.Connection;
using Data.Access.DTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Data.Access.Customer
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<CustomerRepository> _logger;

        public CustomerRepository(IDbConnectionFactory connectionFactory, ILogger<CustomerRepository> logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<long?> AddCustomerAsync(string name, string phoneNumber, string? email, CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT public.fn_add_customer(@p_name, @p_phone_number, @p_email) AS id;";
            _logger.LogInformation("Executing SQL function: {SqlFunction}", "fn_add_customer");

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                var parameters = new { p_name = name, p_phone_number = phoneNumber, p_email = email };

                var newId = await connection.ExecuteScalarAsync<long?>(
                    new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)
                );

                return newId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute SQL function: fn_add_customer for phone {Phone}", phoneNumber);
                return null;
            }
        }

        public async Task<bool> UpdateCustomerAsync(CustomerDTO customer, CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT public.fn_update_customer(@p_customer_id, @p_name, @p_phone_number, @p_email, @p_status);";
            _logger.LogInformation("Executing SQL function: {SqlFunction} for customer ID {Id}", "fn_update_customer", customer.Id);

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                var parameters = new
                {
                    p_customer_id = customer.Id,
                    p_name = customer.Name,
                    p_phone_number = customer.PhoneNumber,
                    p_email = customer.Email,
                    // NOTE: التعديل الأهم: بنبعت الـ Status كـ int مش string
                    p_status = (int)customer.Status
                };

                var result = await connection.ExecuteScalarAsync<bool>(
                    new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)
                );

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute SQL function: fn_update_customer for customer ID {Id}", customer.Id);
                return false;
            }
        }

        public async Task<PagedList<PaginationCustomerDTO>> GetAllCustomersAsync(PaginationParameters parameters, CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT * FROM public.fn_get_all_customers(@p_page_number, @p_page_size, @p_search_term, @p_status);";
            _logger.LogInformation("Executing SQL function: {SqlFunction} with Page {Page}, Size {Size}, Search {Search}, Status {Status}",
                "fn_get_all_customers", parameters.PageNumber, parameters.PageSize, parameters.SearchTerm, parameters.Status);

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

                var dapperParams = new
                {
                    p_page_number = parameters.PageNumber,
                    p_page_size = parameters.PageSize,
                    p_search_term = parameters.SearchTerm,
                    p_status = parameters.Status
                };

                var customers = await connection.QueryAsync<PaginationCustomerDTO>(
                    new CommandDefinition(sql, dapperParams, cancellationToken: cancellationToken)
                );

                var items = customers.ToList();
                long totalCount = items.FirstOrDefault()?.TotalCount ?? 0;

                return new PagedList<PaginationCustomerDTO>(items, totalCount, parameters.PageNumber, parameters.PageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute SQL function: fn_get_all_customers");
                return new PagedList<PaginationCustomerDTO>(new List<PaginationCustomerDTO>(), 0, parameters.PageNumber, parameters.PageSize);
            }
        }

        public async Task<CustomerDTO?> GetCustomerByIdAsync(long customerId, CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT * FROM public.fn_get_customer_by_id(@p_customer_id);";
            _logger.LogInformation("Executing SQL function: {SqlFunction} for ID {Id}", "fn_get_customer_by_id", customerId);

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                var parameters = new { p_customer_id = customerId };

                var customer = await connection.QuerySingleOrDefaultAsync<CustomerDTO>(
                    new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)
                );

                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get customer by ID {Id}", customerId);
                return null;
            }
        }

        public async Task<CustomerDTO?> GetCustomerByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default)
        {
            // NOTE: Dapper هيعمل الـ Mapping أوتوماتيك للـ Enum
            const string sql = "SELECT * FROM public.fn_get_customer_by_phone(@p_phone_number);";
            _logger.LogInformation("Executing SQL function: {SqlFunction} for phone {Phone}", "fn_get_customer_by_phone", phoneNumber);

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                var parameters = new { p_phone_number = phoneNumber };

                var customer = await connection.QuerySingleOrDefaultAsync<CustomerDTO>(
                    new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)
                );

                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get customer by phone {Phone}", phoneNumber);
                return null;
            }
        }

        public async Task<IEnumerable<CustomerDTO>> GetCustomersByStatusAsync(enStatus status, CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT * FROM public.fn_get_customers_by_status(@p_status);";
            _logger.LogInformation("Executing SQL function: {SqlFunction} for status {Status}", "fn_get_customers_by_status", status);

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                var parameters = new { p_status = (int)status };

                var customers = await connection.QueryAsync<CustomerDTO>(
                    new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)
                );

                return customers ?? new List<CustomerDTO>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get customers by status {Status}", status);
                return new List<CustomerDTO>();
            }
        }

        public async Task<bool> UpdateCustomerStatusAsync(long customerId, enStatus Newstatus, enStatus oldStatus ,CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT public.fn_update_customer_status(@p_customer_id, @p_new_status, @p_old_status);";

            _logger.LogInformation("Executing SQL function: {SqlFunction} for customer ID {Id} to {Status}", "fn_update_customer_status", customerId, Newstatus);

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                var parameters = new
                {
                    p_customer_id = customerId,
                    p_new_status = (int)Newstatus,
                    p_old_status = (int)oldStatus
                };

                var result = await connection.ExecuteScalarAsync<bool>(
                    new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)
                );

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute SQL function: fn_update_customer_status for customer ID {Id}", customerId);
                return false;
            }
        }

        public async Task<bool> DeleteCustomerAsync(long customerId, CancellationToken cancellationToken = default)
        {
            // NOTE: لا تحتاج تعديل
            const string sql = "SELECT public.fn_delete_customer(@p_customer_id);";
            _logger.LogInformation("Executing SQL function: {SqlFunction} for customer ID {Id}", "fn_delete_customer", customerId);

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                var parameters = new { p_customer_id = customerId };

                var result = await connection.ExecuteScalarAsync<bool>(
                    new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)
                );

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute SQL function: fn_delete_customer for customer ID {Id}", customerId);
                return false;
            }
        }

        public async Task<CustomerStatisticsDTO?> GetCustomerStatisticsAsync(CancellationToken cancellationToken = default)
        {
            
            const string sql = "SELECT * FROM public.fn_get_customer_statistics();";
            _logger.LogInformation("Executing SQL function: {SqlFunction}", "fn_get_customer_statistics");

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

                var statistics = await connection.QuerySingleOrDefaultAsync<CustomerStatisticsDTO>(
                    new CommandDefinition(sql, cancellationToken: cancellationToken)
                );

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get customer statistics");
                return null;
            }
        }
        private static readonly JsonSerializerOptions _camelCaseOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        public async Task<int?> AddCustomersBulkAsync(IEnumerable<CustomerImportDTO> customers, CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT public.fn_add_customers_bulk(@p_customers_json::jsonb);";
            _logger.LogInformation("Executing SQL function: {SqlFunction} for {Count} customers", "fn_add_customers_bulk", customers.Count());

            try
            {
                var customersJson = JsonSerializer.Serialize(customers, _camelCaseOptions);

                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                var parameters = new { p_customers_json = customersJson };

                var affectedRows = await connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)
                );

                return affectedRows;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute SQL function: fn_add_customers_bulk");
                return null;
            }
        }
    }
}