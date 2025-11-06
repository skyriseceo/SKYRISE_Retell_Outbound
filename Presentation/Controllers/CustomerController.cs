using Data.Access.DTOs;
using Data.Business.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OSV.Models;
using static Data.Business.Data.Requests;

namespace OSV.Controllers
{
    [ApiController]
    [Route("api/customer")]
    [Produces("application/json")]
    [Authorize]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(ICustomerService customerService, ILogger<CustomerController> logger)
        {
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ---------- Create ----------
        [HttpPost("create")]
        [ProducesResponseType(typeof(ApiResponse), 201)]
        [ProducesResponseType(typeof(ApiResponse), 400)]
        [ProducesResponseType(typeof(ApiResponse), 409)]
        public async Task<ActionResult<ApiResponse>> CreateCustomer([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to create customer '{Name}'", request.Name);

            if (request == null)
                return BadRequest(new ApiResponse(false, "Invalid customer request."));

            var newId = await _customerService.CreateCustomerAsync(request.Name, request.phoneNumber, request.Email, cancellationToken);

            if (newId.HasValue)
            {
                var data = new { CustomerId = newId.Value };
                return CreatedAtAction(nameof(GetCustomerById), new { id = newId.Value },
                    new ApiResponse(true, "Customer created successfully.", data));
            }

            return Conflict(new ApiResponse(false, "Phone number or email already exists, or the input data is invalid."));

        }

        // ---------- Update ----------
        [HttpPut("update")]
        [ProducesResponseType(typeof(ApiResponse), 200)]
        [ProducesResponseType(typeof(ApiResponse), 400)]
        [ProducesResponseType(typeof(ApiResponse), 404)]
        public async Task<ActionResult<ApiResponse>> UpdateCustomer([FromBody] CustomerDTO customer, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to update customer ID {Id}", customer.Id);

            if (customer == null || customer.Id <= 0)
                return BadRequest(new ApiResponse(false, "Invalid customer data."));

            var success = await _customerService.UpdateCustomerAsync(customer, cancellationToken);

            if (success)
                return Ok(new ApiResponse(true, "Customer updated successfully."));

            return NotFound(new ApiResponse(false, "Customer not found or update failed."));
        }

        // ---------- Delete ----------
        [HttpDelete("delete/{id:long}")]
        [ProducesResponseType(typeof(ApiResponse), 200)]
        [ProducesResponseType(typeof(ApiResponse), 404)]
        public async Task<ActionResult<ApiResponse>> DeleteCustomer(long id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to delete customer ID {Id}", id);

            if (id <= 0)
                return BadRequest(new ApiResponse(false, "Invalid customer ID."));

            var success = await _customerService.DeleteCustomerAsync(id, cancellationToken);

            if (success)
                return Ok(new ApiResponse(true, "Customer deleted successfully."));

            return NotFound(new ApiResponse(false, "Customer not found Or He have existing bookings."));
        }

        // ---------- Start Call ----------
        [HttpPost("startcall/{id:long}")]
        [ProducesResponseType(typeof(ApiResponse), 200)]
        [ProducesResponseType(typeof(ApiResponse), 404)]
        [ProducesResponseType(typeof(ApiResponse), 500)]
        public async Task<ActionResult<ApiResponse>> StartCall(long id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to start call for customer ID {Id}", id);

            if (id <= 0)
                return BadRequest(new ApiResponse(false, "Invalid customer ID."));

            var success = await _customerService.StartCallAsync(id, cancellationToken);

            if (success)
                return Ok(new ApiResponse(true, "Call initiated successfully."));

            return StatusCode(500, new ApiResponse(false, "Failed to initiate call."));
        }

        // ---------- Get by ID ----------
        [ProducesResponseType(typeof(ApiResponse), 200)]  // <-- تبقى كده
        [ProducesResponseType(typeof(ApiResponse), 404)]
        public async Task<ActionResult<ApiResponse>> GetCustomerById(long id, CancellationToken cancellationToken) // <-- تبقى كده
        {
            // ... (الكود بتاعك زي ما هو) ...
            var customer = await _customerService.GetCustomerByIdAsync(id, cancellationToken);

            if (customer != null)
                // نغلف الـ DTO جوه الـ ApiResponse
                return Ok(new ApiResponse(true, "Customer retrieved successfully.", customer));

            return NotFound(new ApiResponse(false, "Customer not found."));
        }

        // ---------- Paginated ----------
        [HttpGet("paginated")]
        [ProducesResponseType(typeof(PagedList<PaginationCustomerDTO>), 200)]
        [ProducesResponseType(typeof(ApiResponse), 404)]
        public async Task<ActionResult<PagedList<PaginationCustomerDTO>>> GetCustomersPaginated(
            [FromQuery] PaginationParameters parameters,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching paginated customers (Page: {Page}, Search: {Search}, Status: {Status})",
                parameters.PageNumber, parameters.SearchTerm, parameters.Status);

            var customers = await _customerService.GetAllCustomersAsync(parameters, cancellationToken);

            if (customers != null && customers.Items.Any())
                return Ok(customers);

            return NotFound(new ApiResponse(false, "No customers found matching criteria."));
        }

        // ---------- Statistics ----------
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(CustomerStatisticsDTO), 200)]
        [ProducesResponseType(typeof(ApiResponse), 404)]
        public async Task<ActionResult<CustomerStatisticsDTO>> GetCustomerStatistics(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to get customer statistics");

            var stats = await _customerService.GetCustomerStatisticsAsync(cancellationToken);

            if (stats != null)
                return Ok(stats);

            return NotFound(new ApiResponse(false, "Could not retrieve statistics."));
        }

        // ---------- Get by Status ----------
        [HttpGet("status/{status}")]
        [ProducesResponseType(typeof(IEnumerable<CustomerDTO>), 200)]
        [ProducesResponseType(typeof(ApiResponse), 400)]
        [ProducesResponseType(typeof(ApiResponse), 404)]
        public async Task<ActionResult<IEnumerable<CustomerDTO>>> GetCustomersByStatus(enStatus status, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to get customers by status: {Status}", status);

            var customers = await _customerService.GetCustomersByStatusAsync(status, cancellationToken);

            if (customers != null && customers.Any())
                return Ok(customers);

            return NotFound(new ApiResponse(false, $"No customers found with status '{status}'."));
        }
        [HttpPut("send-email")]
        [ProducesResponseType(typeof(ApiResponse), 200)]
        [ProducesResponseType(typeof(ApiResponse), 400)]
        [ProducesResponseType(typeof(ApiResponse), 404)]
        public async Task<ActionResult<ApiResponse>> SendEmailToCustomer([FromBody] SendEmailRequests request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to send email to customer ID {Id}", request.CustomerId);

            if (request == null || request.CustomerId <= 0 || string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
                return BadRequest(new ApiResponse(false, "Invalid email request data."));


            var success = await _customerService.SendEmailToCustomerAsync(request.CustomerId, request.Subject, request.Body, cancellationToken);

            if (success)
                return Ok(new ApiResponse(true, "email sent successfully to customer."));

            return NotFound(new ApiResponse(false, "Customer not found or email sending failed."));
        }
        [HttpPost("import")]
        [ProducesResponseType(typeof(ImportResultDTO), 200)]
        [ProducesResponseType(typeof(ApiResponse), 400)]
        [ProducesResponseType(typeof(ApiResponse), 500)]
        public async Task<IActionResult> ImportCustomers(IFormFile file, CancellationToken cancellationToken)
        {
            const string logPrefix = "Controller: ImportCustomers:";
            _logger.LogInformation("{Prefix} Received customer import request.", logPrefix);

            // 1. التحقق الأساسي من الملف
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("{Prefix} No file provided.", logPrefix);
                return BadRequest(new ApiResponse(false, "No file provided."));
            }

            // 2. (اختياري) التحقق من امتداد الملف
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xls" && extension != ".csv")
            {
                _logger.LogWarning("{Prefix} Invalid file type '{Ext}'.", logPrefix, extension);
                return BadRequest(new ApiResponse(false, "Invalid file type. Only .xlsx, .xls, or .csv are allowed."));
            }

            try
            {
                var result = await _customerService.ImportCustomersAsync(file, cancellationToken);

                if (result.SuccessfullyImported > 0)
                {
                    return Ok(result);
                }

                if (!result.ErrorMessages.Any())
                {
                    return Ok(result);
                }

                return BadRequest(new ApiResponse(false, $"Failed to process file. {string.Join(", ", result.ErrorMessages)}", result.ErrorMessages));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Unhandled exception during import.", logPrefix);
                return StatusCode(500, new ApiResponse(false, "An unexpected server error occurred."));
            }
        }
    }
}
