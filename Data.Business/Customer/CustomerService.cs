using Data.Access.Customer;
using Data.Access.DTOs;
using Data.Business.Booking;
using Data.Business.Data;
using Data.Business.Service.Hubs;
using ExcelDataReader;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Net.Http.Json;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace Data.Business.Customer
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ILogger<CustomerService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHubContext<Service.Hubs.HubNotification, IHubs> _hubContext;
        private readonly IEmailSender _emailSender;
        private readonly IBookingService _bookingService;

        public CustomerService(
            ICustomerRepository customerRepository,
            ILogger<CustomerService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IHubContext<Service.Hubs.HubNotification, IHubs> hubContext,
            IEmailSender emailSender,
            IBookingService bookingService) // <-- (الخطوة 2: إضافة)
        {
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
            _bookingService = bookingService ?? throw new ArgumentNullException(nameof(bookingService)); // <-- (الخطوة 3: إضافة)
        }

        public async Task<long?> CreateCustomerAsync(string name, string phoneNumber, string? email, CancellationToken cancellationToken = default)
        {
            const string logPrefix = "Service: CreateCustomerAsync:";
            _logger.LogInformation("{Prefix} Attempting to create customer '{Name}'", logPrefix, name);

            try
            {
                var existingCustomer = await _customerRepository.GetCustomerByPhoneAsync(phoneNumber, cancellationToken);
                if (existingCustomer != null)
                {
                    _logger.LogWarning("{Prefix} Phone number {Phone} already exists.", logPrefix, phoneNumber);
                    return null;
                }

                long? newId = await _customerRepository.AddCustomerAsync(name, phoneNumber, email, cancellationToken);

                if (newId.HasValue)
                {
                    CustomerDTO? newCustomerDto = await _customerRepository.GetCustomerByIdAsync(newId.Value, cancellationToken);
                    if (newCustomerDto != null)
                    {
                        _logger.LogInformation("{Prefix} Broadcasting new customer {Id} to clients.", logPrefix, newId.Value);
                        await _hubContext.Clients.All.ReceiveNewCustomer(newCustomerDto);
                    }
                }

                return newId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to create customer '{Name}'", logPrefix, name);
                return null;
            }
        }

        public async Task<bool> UpdateCustomerAsync(CustomerDTO customer, CancellationToken cancellationToken = default)
        {
            const string logPrefix = "Service: UpdateCustomerAsync:";
            _logger.LogInformation("{Prefix} Attempting to update customer ID {Id}", logPrefix, customer.Id);
            try
            {
                bool success = await _customerRepository.UpdateCustomerAsync(customer, cancellationToken);

                if (success)
                {
                    _logger.LogInformation("{Prefix} Broadcasting update for customer {Id} to clients.", logPrefix, customer.Id);

                    CustomerDTO? updatedDto = await _customerRepository.GetCustomerByIdAsync(customer.Id, cancellationToken);
                    if (updatedDto != null)
                    {
                        await _hubContext.Clients.All.ReceiveCustomerUpdate(updatedDto);
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to update customer ID {Id}", logPrefix, customer.Id);
                return false;
            }
        }

        public async Task<bool> DeleteCustomerAsync(long customerId, CancellationToken cancellationToken = default)
        {
            const string logPrefix = "Service: DeleteCustomerAsync:";
            _logger.LogInformation("{Prefix} Attempting to delete customer ID {Id}", logPrefix, customerId);
            try
            {
                bool success = await _customerRepository.DeleteCustomerAsync(customerId, cancellationToken);

                if (success)
                {
                    _logger.LogInformation("{Prefix} Broadcasting deletion for customer {Id} to clients.", logPrefix, customerId);
                    await _hubContext.Clients.All.ReceiveCustomerDeletion(customerId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to delete customer ID {Id}", logPrefix, customerId);
                return false;
            }
        }

        public async Task<bool> StartCallAsync(long customerId, CancellationToken cancellationToken = default)
        {
            const string logPrefix = "Service: StartCallAsync:";
            _logger.LogInformation("{Prefix} Attempting to start call for customer ID {Id}", logPrefix, customerId);

            CustomerDTO? customer;
            try
            {
                customer = await _customerRepository.GetCustomerByIdAsync(customerId, cancellationToken);
                if (customer == null)
                {
                    _logger.LogError("{Prefix} Customer not found for ID {Id}", logPrefix, customerId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to get customer by ID {Id}", logPrefix, customerId);
                return false;
            }

            var retellApiKey = _configuration["Retell:ApiKey"];
            var retellAgentId = _configuration["Retell:AgentId"];
            var retellFromNumber = _configuration["Retell:FromNumber"];
            var baseUrl = _configuration["AppSettings:BaseUrl"];

            var webhookUrl = $"{baseUrl}/api/v1/webhook/retell-updates";

            // --- 2. التحقق من الإعدادات ---
            if (string.IsNullOrWhiteSpace(retellApiKey) ||
                string.IsNullOrWhiteSpace(retellAgentId) ||
                string.IsNullOrWhiteSpace(retellFromNumber) ||
                string.IsNullOrWhiteSpace(baseUrl))
            {
                _logger.LogError("{Prefix} Retell ApiKey, AgentId, FromNumber, or BaseUrl is not set. Customer ID {Id}", logPrefix, customerId);
                await UpdateAndBroadcastStatus(customer, enStatus.Failed, customer.Status, cancellationToken);
                return false;
            }

            try
            {
                if (customer.Status != enStatus.Calling)
                {
                    var statusUpdated = await _customerRepository.UpdateCustomerStatusAsync(customerId, enStatus.Calling, customer.Status, cancellationToken);
                    if (!statusUpdated)
                    {
                        _logger.LogWarning("{Prefix} Failed to update status to Calling for ID {Id}. Status already changed (Race Condition) or DB issue.", logPrefix, customerId);
                        return false;
                    }
                    customer.Status = enStatus.Calling;
                    await _hubContext.Clients.All.ReceiveCustomerUpdate(customer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to update customer status for ID {Id}", logPrefix, customerId);
                return false;
            }


            try
            {
                var client = _httpClientFactory.CreateClient("RetellClient");

                var requestBody = new
                {
                    agent_id = retellAgentId,
                    to_number = customer.PhoneNumber,
                    from_number = retellFromNumber,
                    retell_llm_dynamic_variables = new { customer_name = customer.Name },
                    call_status_updates_url = webhookUrl,
                    metadata = new { our_customer_id = customer.Id }
                };

                var response = await client.PostAsJsonAsync("v2/create-phone-call", requestBody, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("{Prefix} Retell API (v2/calls) failed for customer {Id}: {Reason}", logPrefix, customerId, errorContent);
                    await UpdateAndBroadcastStatus(customer, enStatus.Failed, customer.Status, cancellationToken);
                    return false;
                }
                _logger.LogInformation("{Prefix} Retell API call (v2/calls) initiated successfully for {Phone} (Customer ID {Id})", logPrefix, customer.PhoneNumber, customerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to call Retell API (v2/calls) for customer ID {Id}", logPrefix, customerId);
                await UpdateAndBroadcastStatus(customer, enStatus.Failed, customer.Status, cancellationToken);
                return false;
            }
        }

        public async Task<bool> HandleWebhookCallbackAsync(Requests.RetellWebhookEnvelope envelope, CancellationToken cancellationToken = default)
        {
            const string logPrefix = "Service: HandleWebhookCallback:";
            if (envelope.Event != "call_analyzed")
            {
                _logger.LogInformation(
                    "{Prefix} Ignoring event '{Event}' for CallID {CallId}. Only processing 'call_analyzed'.",
                    logPrefix, envelope.Event, envelope.Call?.CallId ?? "N/A");
                return true;
            }
            if (envelope.Call == null || envelope.Call.Analysis == null || envelope.Call.Metadata == null)
            {
                _logger.LogWarning(
                    "{Prefix} Received 'call_analyzed' event but payload (Call, Analysis, or Metadata) is missing. CallID: {CallId}",
                    logPrefix, envelope.Call?.CallId ?? "N/A");
                return false;
            }

            var payload = envelope.Call;

            long customerId = payload.Metadata.OurCustomerId;
            string retellCallId = payload.CallId;
            bool isSuccessful = payload.Analysis.CallSuccessful;
            string disconnectReason = payload.DisconnectionReason?.ToLowerInvariant() ?? string.Empty;
            string rootCallStatus = payload.CallStatus?.ToLowerInvariant() ?? string.Empty;

            _logger.LogInformation(
                "{Prefix} Handling 'call_analyzed' webhook for Customer ID {Id} (CallID: {CallId}). Successful: {Success}, Reason: {Reason}, Status: {Status}",
                logPrefix, customerId, retellCallId, isSuccessful, disconnectReason, rootCallStatus);

            if (rootCallStatus != "completed")
            {
                _logger.LogWarning("{Prefix} Received 'call_analyzed' but status is '{Status}' (expected 'completed'). Ignoring...",
                    logPrefix, rootCallStatus, retellCallId);
                return true;
            }

            try
            {
                var customer = await _customerRepository.GetCustomerByIdAsync(customerId, cancellationToken);
                if (customer == null)
                {
                    _logger.LogError("{Prefix} Webhook received for unknown Customer ID {Id} (CallID: {CallId})",
                        logPrefix, customerId, retellCallId);
                    return false;
                }

                enStatus newStatus;

                if (isSuccessful)
                {
                    if (customer.Status == enStatus.Booked)
                    {
                        newStatus = enStatus.Booked;
                        _logger.LogInformation("{Prefix} Call successful. Customer {Id} is already 'Booked'. No status change.",
                            logPrefix, customerId);
                    }
                    else
                    {
                        newStatus = enStatus.Contacted;
                        _logger.LogInformation("{Prefix} Call successful. Customer {Id} status set to 'Contacted'.",
                            logPrefix, customerId);
                    }
                }
                else
                {
                    switch (disconnectReason)
                    {
                        case "user_hangup":
                        case "agent_hangup":
                            newStatus = enStatus.Contacted;
                            break;

                        case "call_failed":
                        case "invalid_phone_number":
                            newStatus = enStatus.Failed;
                            break;

                        case "user_not_answered":
                        case "no_answer":
                            newStatus = enStatus.NoAnswer;
                            break;

                        case "user_busy":
                            newStatus = enStatus.NoAnswer;
                            break;

                        default:
                            newStatus = enStatus.Failed;
                            _logger.LogWarning("{Prefix} Unhandled disconnect reason '{Reason}'. Defaulting to Failed for Customer {Id}.",
                                logPrefix, disconnectReason, customerId);
                            break;
                    }
                }

                if (customer.Status == newStatus)
                {
                    _logger.LogWarning("{Prefix} Status is already {Status} for customer {Id}.",
                        logPrefix, newStatus, customer.Id);
                    return true;
                }

                bool updateSuccess = await _customerRepository.UpdateCustomerStatusAsync(
                    customer.Id, newStatus, customer.Status, cancellationToken);

                if (updateSuccess)
                {
                    _logger.LogInformation("{Prefix} Status updated to {Status}. Broadcasting update for customer {Id}.",
                        logPrefix, newStatus, customer.Id);
                    customer.Status = newStatus;
                    await _hubContext.Clients.All.ReceiveCustomerUpdate(customer);
                }
                else
                {
                    _logger.LogError("{Prefix} Failed to update status in DB for customer {Id} from {OldStatus} to {NewStatus}.",
                        logPrefix, customer.Id, customer.Status, newStatus);
                }

                return updateSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to handle webhook for Customer ID {Id} (CallID: {CallId})",
                    logPrefix, customerId, retellCallId);
                return false;
            }
        }


        public async Task<CustomerDTO?> GetCustomerByIdAsync(long customerId, CancellationToken cancellationToken = default)
        {
            return await _customerRepository.GetCustomerByIdAsync(customerId, cancellationToken);
        }

        public async Task<PagedList<PaginationCustomerDTO>> GetAllCustomersAsync(PaginationParameters parameters, CancellationToken cancellationToken = default)
        {
            return await _customerRepository.GetAllCustomersAsync(parameters, cancellationToken);
        }

        public async Task<IEnumerable<CustomerDTO>> GetCustomersByStatusAsync(enStatus status, CancellationToken cancellationToken = default)
        {
            return await _customerRepository.GetCustomersByStatusAsync(status, cancellationToken);
        }

        public async Task<CustomerDTO?> GetCustomerByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default)
        {
            return await _customerRepository.GetCustomerByPhoneAsync(phoneNumber, cancellationToken);
        }

        public async Task<bool> UpdateAndBroadcastStatus(CustomerDTO customer, enStatus newStatus, enStatus oldStatus, CancellationToken cancellationToken)
        {
            try
            {
                if (customer.Status == newStatus) return false;

                bool updateSuccess = await _customerRepository.UpdateCustomerStatusAsync(customer.Id, newStatus, oldStatus, cancellationToken);
                if (updateSuccess)
                {
                    customer.Status = newStatus;
                    await _hubContext.Clients.All.ReceiveCustomerUpdate(customer);
                }
                return updateSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed in UpdateAndBroadcastStatus for customer {Id}", customer.Id);
                return false;
            }
        }
        public async Task<CustomerStatisticsDTO?> GetCustomerStatisticsAsync(CancellationToken cancellationToken = default)
        {
            return await _customerRepository.GetCustomerStatisticsAsync(cancellationToken);
        }


        public async Task<ImportResultDTO> ImportCustomersAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            const string logPrefix = "Service: ImportCustomersAsync:";
            _logger.LogInformation("{Prefix} Starting customer import (BULK MODE).", logPrefix);

            var result = new ImportResultDTO();
            var customersToInsert = new List<CustomerImportDTO>();

            // <--- (الخطوة 1: إضافة HashSet لتتبع الأرقام داخل الملف)
            var phoneNumbersInFile = new HashSet<string>();

            try
            {
                using var stream = file.OpenReadStream();
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                using IExcelDataReader reader = extension == ".csv"
                    ? ExcelReaderFactory.CreateCsvReader(stream)
                    : ExcelReaderFactory.CreateReader(stream);

                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                {
                    ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
                });

                if (dataSet.Tables.Count == 0)
                {
                    _logger.LogWarning("{Prefix} Excel file is empty.", logPrefix);
                    result.ErrorMessages.Add("File is empty or unreadable.");
                    return result;
                }

                var dataTable = dataSet.Tables[0];
                result.TotalRows = dataTable.Rows.Count;

                // --- 1. البحث عن العواميد بمرونة ---
                int nameColIdx = FindColumnIndex(dataTable.Columns, "name", "full name", "customer name");
                int phoneColIdx = FindColumnIndex(dataTable.Columns, "phone", "phone number", "phonenumber");
                int emailColIdx = FindColumnIndex(dataTable.Columns, "email", "email address", "e-mail");

                if (nameColIdx == -1 || phoneColIdx == -1)
                {
                    _logger.LogError("{Prefix} Invalid headers. File must contain 'Name' and 'Phone' columns.", logPrefix);
                    result.ErrorMessages.Add("File must contain columns for 'Name' and 'Phone'. Case or spacing doesn't matter.");
                    return result;
                }

                int rowNumber = 2;
                foreach (DataRow row in dataTable.Rows)
                {
                    try
                    {
                        string? name = row[nameColIdx]?.ToString()?.Trim();
                        string? phone = row[phoneColIdx]?.ToString()?.Trim();
                        string? email = emailColIdx != -1 ? row[emailColIdx]?.ToString()?.Trim() : null;

                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone))
                        {
                            result.ErrorMessages.Add($"Row {rowNumber}: 'Name' and 'Phone' are required. Skipping this row.");
                            continue;
                        }

                        // <--- (الخطوة 2: التحقق من التكرار داخل الملف)
                        // phoneNumbersInFile.Add(phone) هترجع false لو الرقم موجود قبل كدة
                        if (!phoneNumbersInFile.Add(phone))
                        {
                            _logger.LogWarning("{Prefix} Skipping Row {RowNumber}: Phone number '{Phone}' is duplicated within the file.", logPrefix, rowNumber, phone);
                            result.ErrorMessages.Add($"Row {rowNumber}: Phone number '{phone}' is duplicated in the file. Skipping this row.");
                            continue; // تجاهل الصف ده وكمل
                        }
                        // <--- (نهاية التعديل)

                        customersToInsert.Add(new CustomerImportDTO
                        {
                            Name = name,
                            PhoneNumber = phone,
                            Email = email
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "{Prefix} Failed to process row {RowNumber}. Skipping.", logPrefix, rowNumber);
                        result.ErrorMessages.Add($"Row {rowNumber}: Contains invalid data and could not be read.");
                    }
                    finally
                    {
                        rowNumber++;
                    }
                }

                if (!customersToInsert.Any())
                {
                    _logger.LogWarning("{Prefix} No valid rows found to import.", logPrefix);
                    if (!result.ErrorMessages.Any())
                        result.ErrorMessages.Add("No valid customer rows found in the file.");

                    result.FailedOrDuplicates = result.TotalRows;
                    return result;
                }

                // (الكود ده زي ما هو، هيبعت بس القائمة المنقحة بدون تكرار)
                var affectedRows = await _customerRepository.AddCustomersBulkAsync(customersToInsert, cancellationToken);

                if (affectedRows.HasValue)
                {
                    result.SuccessfullyImported = affectedRows.Value;
                    // نحسب الفشل بناءً على عدد الصفوف اللي حاولت تدخل الداتابيز
                    result.FailedOrDuplicates = customersToInsert.Count - result.SuccessfullyImported;


                    result.FailedOrDuplicates = result.TotalRows - result.SuccessfullyImported;
                }
                else
                {
                    _logger.LogError("{Prefix} Bulk import failed. Repository returned null.", logPrefix);
                    result.ErrorMessages.Add("Database operation failed during bulk insert.");
                    result.FailedOrDuplicates = customersToInsert.Count; // <--- تعديل: الفشل هنا خاص بالصفوف اللي اتبعتت
                }

                if (result.SuccessfullyImported > 0)
                {
                    _logger.LogInformation("{Prefix} Import complete. Broadcasting a generic 'ReceiveCustomerUpdate' message.", logPrefix);
                    await _hubContext.Clients.All.ReceiveCustomersImported();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed during customer import.", logPrefix);
                result.ErrorMessages.Add($"An unexpected error occurred: {ex.Message}");
            }

            // <--- (تعديل بسيط في اللوج عشان يبقى أوضح)
            _logger.LogInformation("{Prefix} Bulk import finished. Total Rows in file: {Total}, Rows sent to DB: {Sent}, Success: {Success}, Failed/DB Duplicates: {Failed}",
                logPrefix, result.TotalRows, customersToInsert.Count, result.SuccessfullyImported, result.FailedOrDuplicates);

            return result;
        }

        private int FindColumnIndex(DataColumnCollection columns, params string[] possibleNames)
        {
            foreach (DataColumn column in columns)
            {
                var normalizedColumnName = Normalize(column.ColumnName);

                foreach (var name in possibleNames)
                {
                    var normalizedTarget = Normalize(name);

                    if (string.Equals(normalizedColumnName, normalizedTarget, StringComparison.OrdinalIgnoreCase))
                        return column.Ordinal;
                }
            }
            return -1;
        }

        private string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // نخلي فقط الحروف والأرقام، ونحولها لحروف صغيرة
            return new string(input
                .Trim()
                .ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c))
                .ToArray());
        }



        public async Task<bool> SendEmailToCustomerAsync(long customerId, string subject, string body, CancellationToken cancellationToken = default)
        {
            const string logPrefix = "Service: SendEmailToCustomerAsync:";
            _logger.LogInformation("{Prefix} Attempting to send email to customer ID {Id}", logPrefix, customerId);

            try
            {
                var customer = await _customerRepository.GetCustomerByIdAsync(customerId, cancellationToken);
                if (customer == null)
                {
                    _logger.LogWarning("{Prefix} Customer not found for ID {Id}. email not sent.", logPrefix, customerId);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(customer.Email))
                {
                    _logger.LogWarning("{Prefix} Customer {Id} ({Name}) has no email address. email not sent.", logPrefix, customerId, customer.Name);
                    return false;
                }
                await _emailSender.SendEmailAsync(customer.Email, subject, body);

                _logger.LogInformation("{Prefix} email successfully queued for sending to customer {Id} (email: {email})", logPrefix, customerId, customer.Email);
                return true;
            }
            catch (Exception ex)
            {
                // (الـ AwsSesEmailService سيرمي Exception في حالة الفشل، ونحن نلتقطه هنا)
                _logger.LogError(ex, "{Prefix} Failed to send email to customer ID {Id}", logPrefix, customerId);
                return false;
            }
        }


    }
}