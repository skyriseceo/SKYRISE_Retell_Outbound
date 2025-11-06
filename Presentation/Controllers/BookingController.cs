using Data.Access.DTOs;
using Data.Business.Booking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OSV.Models;


namespace OSV.Controllers
{
    [ApiController]
    [Route("api/booking")]
    [Produces("application/json")]
    [Authorize]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly ILogger<BookingController> _logger;

        public BookingController(IBookingService bookingService, ILogger<BookingController> logger)
        {
            _bookingService = bookingService ?? throw new ArgumentNullException(nameof(bookingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("create")]
        [ProducesResponseType(typeof(ApiResponse), 201)]
        [ProducesResponseType(typeof(ApiResponse), 400)]
        [ProducesResponseType(typeof(ApiResponse), 409)]
        public async Task<ActionResult<ApiResponse>> CreateBooking([FromBody] BookingDTO request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to create booking for CallId '{CallId}'", request.CallId);

            if (request == null)
                return BadRequest(new ApiResponse(false, "Invalid booking request."));

            var newId = await _bookingService.CreateBookingAsync(request, cancellationToken);

            if (newId.HasValue && newId.Value > 0)
            {
                var data = new { BookingId = newId.Value };
                return CreatedAtAction(nameof(GetBookingById), new { id = newId.Value },
                    new ApiResponse(true, "Booking created successfully.", data));
            }

            return Conflict(new ApiResponse(false, "Failed to create booking. Check for duplicate or invalid data."));
        }

        // ---------- Get By Id ----------
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(BookingDTO), 200)]
        [ProducesResponseType(typeof(ApiResponse), 404)]
        public async Task<ActionResult<BookingDTO>> GetBookingById(long id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to get booking ID {Id}", id);

            if (id <= 0)
                return BadRequest(new ApiResponse(false, "Invalid booking ID."));

            var booking = await _bookingService.GetBookingByIdAsync(id, cancellationToken);

            if (booking != null)
                return Ok(booking);

            return NotFound(new ApiResponse(false, "Booking not found."));
        }

        // ---------- Get Paginated ----------
        [HttpGet("paginated")]
        [ProducesResponseType(typeof(PagedList<PaginationBookingDTO>), 200)]
        [ProducesResponseType(typeof(ApiResponse), 404)]
        public async Task<ActionResult<PagedList<PaginationBookingDTO>>> GetBookingsPaginated(
            [FromQuery] PaginationParameters parameters,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching bookings (Page: {Page}, Search: {Search}, Status: {Status})",
                parameters.PageNumber, parameters.SearchTerm, parameters.Status);

            var bookings = await _bookingService.GetBookingsPaginatedAsync(parameters, cancellationToken);

            if (bookings != null && bookings.Items.Any())
                return Ok(bookings);

            return NotFound(new ApiResponse(false, "No bookings found matching criteria."));
        }

        // ---------- Update Status ----------
        [HttpPut("update-status/{id:long}")]
        [ProducesResponseType(typeof(ApiResponse), 200)]
        [ProducesResponseType(typeof(ApiResponse), 400)]
        [ProducesResponseType(typeof(ApiResponse), 404)]
        public async Task<ActionResult<ApiResponse>> UpdateBookingStatus(long id, [FromQuery] enBookingStatus status, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to update status for booking ID {Id} to {Status}", id, status);

            if (id <= 0)
                return BadRequest(new ApiResponse(false, "Invalid booking ID."));

            var success = await _bookingService.UpdateBookingStatusAsync(id, status, cancellationToken);

            if (success)
                return Ok(new ApiResponse(true, $"Booking status updated to '{status}' successfully."));

            return NotFound(new ApiResponse(false, "Booking not found or update failed."));
        }

        // ---------- Statistics ----------
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(BookingStatisticsDTO), 200)]
        [ProducesResponseType(typeof(ApiResponse), 404)]
        public async Task<ActionResult<BookingStatisticsDTO>> GetBookingStatistics(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to get booking statistics");

            var stats = await _bookingService.GetBookingStatisticsAsync(cancellationToken);

            if (stats != null)
                return Ok(stats);

            return NotFound(new ApiResponse(false, "Could not retrieve booking statistics."));
        }
    }
}
