using Data.Access.Booking;
using Data.Access.Customer;
using Data.Access.DTOs;
using Data.Business.Service.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OSV.Attributes; // (تأكد من إضافة الـ using للفلتر الخاص بك)

namespace OSV.Controllers
{
    [ApiController]
    [Route("api/v1/webhooks")]
    public class CalComWebhookController : ControllerBase
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IHubContext<HubNotification, IHubs> _hubContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CalComWebhookController> _logger;

        public CalComWebhookController(
            IBookingRepository bookingRepository,
            ICustomerRepository customerRepository,
            IHubContext<HubNotification, IHubs> hubContext,
            IConfiguration configuration,
            ILogger<CalComWebhookController> logger)
        {
            _bookingRepository = bookingRepository;
            _customerRepository = customerRepository;
            _hubContext = hubContext;
            _configuration = configuration;
            _logger = logger;
        }


        [HttpPost("calcom")]
        [AllowAnonymous]
        [TypeFilter(typeof(CalComSignatureAuthFilter))]
        public async Task<IActionResult> HandleCalComWebhook(
            [FromBody] CalComWebhookPayload payload)
        {
            const string logPrefix = "CalComWebhook:";

            _logger.LogInformation("{Prefix} Received webhook event: {Event}", logPrefix, payload.TriggerEvent);

            try
            {
                switch (payload.TriggerEvent)
                {
                    case "BOOKING_CREATED":
                        return await HandleBookingCreated(payload.Payload);

                    case "BOOKING_CANCELLED":
                        return await HandleBookingCancelled(payload.Payload);

                    case "BOOKING_RESCHEDULED":
                        return await HandleBookingRescheduled(payload.Payload);

                    default:
                        _logger.LogInformation("{Prefix} Ignoring unhandled event: {Event}.", logPrefix, payload.TriggerEvent);
                        return Ok();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to process Cal.com webhook event {Event}.", logPrefix, payload.TriggerEvent);
                return StatusCode(500, new { Message = "Internal server error while processing webhook." });
            }
        }


        private async Task<IActionResult> HandleBookingCreated(WebhookPayloadData bookingData)
        {
            const string logPrefix = "CalComWebhook:Created:";
            var attendee = bookingData.Attendees.FirstOrDefault();

            if (attendee == null)
            {
                _logger.LogWarning("{Prefix} Booking UID {Uid} received without attendees. Ignoring.", logPrefix, bookingData.Uid);
                return BadRequest(new { Message = "Attendee information is missing." });
            }

            enBookingStatus localStatus = MapCalStatusToLocalStatus(bookingData.Status);

            var newBookingDto = new BookingDTO
            {
                ProspectName = attendee.Name,
                ProspectEmail = attendee.Email,
                ProspectPhone = null, // (كما هو متوقع، لا يوجد رقم هاتف)
                AppointmentTime = bookingData.StartTime,
                Status = localStatus,
                AgentId = "Cal.com",
                CallId = bookingData.Uid
            };

            var newBookingId = await _bookingRepository.AddBookingAsync(newBookingDto);

            if (newBookingId == 0)
            {
                _logger.LogWarning("{Prefix} Failed to add booking, possibly duplicate call_id {CallId}.", logPrefix, newBookingDto.CallId);
                return Conflict(new { Message = "Duplicate booking or database error." });
            }

            var booking = await _bookingRepository.GetBookingByIdAsync(newBookingId);

            if (booking != null && booking.CustomerId != null)
            {
                var customer = await _customerRepository.GetCustomerByIdAsync(booking.CustomerId.Value);
                if (customer != null)
                {
                    await _hubContext.Clients.All.ReceiveBookingUpdate(booking, "Confirmed");
                    await _hubContext.Clients.All.ReceiveCustomerUpdate(customer);
                }
            }

            _logger.LogInformation("{Prefix} Successfully processed booking. New local Booking ID: {Id}", logPrefix, newBookingId);
            return Ok(new { BookingId = newBookingId });
        }

        /// <summary>
        /// يعالج إلغاء الحجز ويقوم بتحديث حالة العميل.
        /// </summary>
        private async Task<IActionResult> HandleBookingCancelled(WebhookPayloadData bookingData)
        {
            const string logPrefix = "CalComWebhook:Cancelled:";

            var success = await _bookingRepository.UpdateBookingStatusByCallIdAsync(
                bookingData.Uid,
                enBookingStatus.Cancelled,
                default
            );

            if (!success)
            {
                _logger.LogWarning("{Prefix} Failed to find or cancel booking for CallId {CallId}.", logPrefix, bookingData.Uid);
                return NotFound(new { Message = "Booking not found." });
            }

            var booking = await _bookingRepository.GetBookingByCallIdAsync(bookingData.Uid);
            if (booking == null || booking.CustomerId == null)
            {
                _logger.LogError("{Prefix} Booking or CustomerId not found after cancel for CallId {CallId}.", logPrefix, bookingData.Uid);
                return NotFound(new { Message = "Booking data inconsistent after cancel." });
            }

            var customer = await _customerRepository.GetCustomerByIdAsync(booking.CustomerId.Value, default);

            // 3. (هام) تحديث حالة العميل رجوعًا إلى "Contacted"
            bool statusUpdated = await _customerRepository.UpdateCustomerStatusAsync(customer.Id, enStatus.Contacted, enStatus.Booked, default);
            if (!statusUpdated)
            {
                _logger.LogWarning("{Prefix} Failed to update customer status from Booked to Contacted for CustomerId {Id}.", logPrefix, customer.Id);
            }

            var updatedCustomer = await _customerRepository.GetCustomerByIdAsync(booking.CustomerId.Value);

            await _hubContext.Clients.All.ReceiveBookingUpdate(booking, "Cancelled");
            if (updatedCustomer != null)
                await _hubContext.Clients.All.ReceiveCustomerUpdate(updatedCustomer);

            _logger.LogInformation("{Prefix} Successfully cancelled booking for CallId {CallId}.", logPrefix, bookingData.Uid);
            return Ok(new { Message = "Booking cancelled successfully." });
        }

        /// <summary>
        /// يعالج إعادة جدولة الموعد.
        /// </summary>
        private async Task<IActionResult> HandleBookingRescheduled(WebhookPayloadData bookingData)
        {
            const string logPrefix = "CalComWebhook:Rescheduled:";

            var success = await _bookingRepository.RescheduleBookingByCallIdAsync(
                bookingData.Uid,
                bookingData.StartTime, // (الموعد الجديد)
                default
            );

            if (!success)
            {
                _logger.LogWarning("{Prefix} Failed to find or reschedule booking for CallId {CallId}.", logPrefix, bookingData.Uid);
                return NotFound(new { Message = "Booking not found." });
            }

            var booking = await _bookingRepository.GetBookingByCallIdAsync(bookingData.Uid);

            if (booking != null)
                await _hubContext.Clients.All.ReceiveBookingUpdate(booking, "Confirmed");

            _logger.LogInformation("{Prefix} Successfully rescheduled booking for CallId {CallId} to {NewTime}.", logPrefix, bookingData.Uid, bookingData.StartTime);
            return Ok(new { Message = "Booking rescheduled successfully." });
        }

        /// <summary>
        /// دالة مساعدة لترجمة حالات Cal.com النصية إلى enBookingStatus الرقمي.
        /// </summary>
        private enBookingStatus MapCalStatusToLocalStatus(string calStatus)
        {
            switch (calStatus?.ToLowerInvariant())
            {
                case "accepted":
                case "upcoming":
                case "recurring":
                case "past":
                    return enBookingStatus.Confirmed; // 1

                case "cancelled":
                    return enBookingStatus.Cancelled; // 2

                case "pending":
                case "unconfirmed":
                    return enBookingStatus.Pending;   // 0

                default:
                    _logger.LogWarning("Unknown Cal.com status received: {Status}. Defaulting to Pending.", calStatus);
                    return enBookingStatus.Pending;   // 0
            }
        }
    }
}