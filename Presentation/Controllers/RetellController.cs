using Data.Business.Booking;
using Data.Business.Customer;
using Data.Business.Data;
using Microsoft.AspNetCore.Mvc;
using OSV.Attributes;
using OSV.Models;


namespace OSV.Controllers
{
    [ApiController]
    [Route("api/v1/webhook")]
    [Produces("application/json")]
    public class RetellController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly ILogger<RetellController> _logger;
        private readonly IBookingService _bookingService;
        private readonly IConfiguration _configuration; // (لازم ده يكون موجود)

        public RetellController(
            ICustomerService customerService,
            ILogger<RetellController> logger,
            IBookingService bookingService,
            IConfiguration configuration // (ضيف ده لو مش موجود)
            )
        {
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bookingService = bookingService ?? throw new ArgumentNullException(nameof(bookingService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration)); // (ضيف ده لو مش موجود)
        }

        // -----------------------------------------------------------------
        // (1) Endpoint الحجز الفوري (ده اللي إنت عايزه)
        // -----------------------------------------------------------------
        //[HttpPost("create")] // (الـ Route بتاعك سليم)
        //[ApiKeyAuth] // (التأمين سليم)
        //[ProducesResponseType(typeof(ApiResponse), 200)]
        //[ProducesResponseType(typeof(ApiResponse), 400)]
        //[ProducesResponseType(typeof(ApiResponse), 500)]
        //public async Task<IActionResult> CreateBooking([FromBody] RetellFunctionPayload payload, CancellationToken cancellationToken)
        //{
        //    const string logPrefix = "Controller: CreateBooking:";

        //    var toolArgs = payload?.Args;

        //    var callId = payload?.Call?.CallId;

        //    var customerId = payload?.Call?.Metadata?.OurCustomerId;

        //    if (toolArgs == null || toolArgs.Datetime == DateTime.MinValue)
        //    {
        //        _logger.LogWarning("{Prefix} Invalid booking arguments (args).", logPrefix);
        //        return BadRequest(new ApiResponse(false, "Invalid booking data (args)."));
        //    }
        //    if (string.IsNullOrEmpty(callId) || customerId == null || customerId.Value == 0)
        //    {
        //        _logger.LogWarning("{Prefix} CallId or CustomerId missing from Function payload (call or metadata).", logPrefix);
        //        return BadRequest(new ApiResponse(false, "Invalid call metadata."));
        //    }

        //    _logger.LogInformation("{Prefix} Real-time booking request received for CallId {CallId}, Customer {CustomerId}", logPrefix, callId, customerId.Value);

        //    try
        //    {

        //        var newBookingDto = new BookingDTO
        //        {
        //            AppointmentTime = toolArgs.Datetime.ToUniversalTime(),
        //            ProspectName = toolArgs.CustomerName,
        //            ProspectEmail = toolArgs.Email,
        //            ProspectPhone = toolArgs.PhoneNumber,
        //            CallId = callId,
        //            CustomerId = customerId.Value, 
        //            Status = enBookingStatus.Booked,
        //            AgentId = _configuration["Retell:AgentId"]
        //        };

        //        var newBookingId = await _bookingService.CreateBookingAsync(newBookingDto, cancellationToken);

        //        if (newBookingId.HasValue && newBookingId.Value > 0)
        //        {
        //            _logger.LogInformation("{Prefix} Successfully created and linked booking {BookingId} for CallId {CallId}", logPrefix, newBookingId.Value, callId);

        //            var customer = await _customerService.GetCustomerByIdAsync(customerId.Value, cancellationToken);
        //            if (customer != null && customer.Status != enStatus.Booked && customerId.HasValue)
        //            {
        //                bool updateSuccess = await _customerService.UpdateAndBroadcastStatus(customer, enStatus.Booked, customer.Status, cancellationToken);
        //                if (!updateSuccess)
        //                {
        //                    _logger.LogWarning("{Prefix} Booking {BookingId} created, but failed to update customer {CustomerId} status.", logPrefix, newBookingId.Value, customerId.Value);
        //                }
        //            }

        //            return Ok(new ApiResponse(true, "Booking created successfully.", new { BookingId = newBookingId.Value }));
        //        }

        //        _logger.LogError("{Prefix} Failed to save booking to database for CallId {CallId}.", logPrefix, callId);
        //        return Conflict(new ApiResponse(false, "Failed to create booking. Check for duplicate or invalid data."));
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "{Prefix} Error processing real-time booking for CallId {CallId}", logPrefix, callId);
        //        return StatusCode(500, new ApiResponse(false, "Server error."));
        //    }
        //}

        // -----------------------------------------------------------------
        // (2) Endpoint ملخص نهاية المكالمة (للحالات الفاشلة)
        // (ده سليم زي ما هو، عشان يعالج NoAnswer, Failed, etc.)
        // -----------------------------------------------------------------
        [HttpPost("retell-updates")]
        [RetellSignatureAuth]
        public async Task<IActionResult> HandleRetellWebhook(
     [FromBody] Requests.RetellWebhookEnvelope envelope,
     CancellationToken cancellationToken)
        {
            if (envelope?.Call == null || envelope.Call.Metadata == null || envelope.Call.Metadata.OurCustomerId == 0)
            {
                _logger.LogWarning("Webhook: Received invalid or incomplete envelope or call data.");
                return BadRequest(new ApiResponse(false, "Invalid payload. 'call' or 'metadata' missing."));
            }

            var payload = envelope.Call;

            bool success = await _customerService.HandleWebhookCallbackAsync(envelope, cancellationToken);

            if (!success)
            {
                _logger.LogError("Webhook: Service failed to process payload for CallID {CallId}. Returning 500.", payload.CallId);
                return StatusCode(500, new ApiResponse(false, "Failed to process webhook. Service error."));
            }

            _logger.LogInformation("Webhook: Successfully processed Retell event '{Event}' for CallID {CallId}.",
                envelope.Event, payload.CallId);

            return Ok(new { success = true, message = "Webhook processed successfully." });
        }

    }
}