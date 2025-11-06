using Data.Access.Booking;
using Data.Access.DTOs;
using Data.Business.Service.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;


namespace Data.Business.Booking
{
    public class BookingService : IBookingService
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly ILogger<BookingService> _logger;
        private readonly IHubContext<Service.Hubs.HubNotification, IHubs> _hubContext;

        public BookingService(
            IBookingRepository bookingRepository,
            ILogger<BookingService> logger,
            IHubContext<Service.Hubs.HubNotification, IHubs> hubContext)
        {
            _bookingRepository = bookingRepository ?? throw new ArgumentNullException(nameof(bookingRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task<long?> CreateBookingAsync(BookingDTO request, CancellationToken cancellationToken = default)
        {
            const string logPrefix = "Service: CreateBookingAsync:";
            _logger.LogInformation("{Prefix} Attempting to create booking for CallId '{CallId}'", logPrefix, request.CallId);

            try
            {
                if (request.AppointmentTime < DateTime.UtcNow)
                {
                    _logger.LogWarning("{Prefix} Appointment time {Time} is in the past.", logPrefix, request.AppointmentTime);
                    return null;
                }

                if (string.IsNullOrEmpty(request.CallId))
                {
                    _logger.LogWarning("{Prefix} CallId is null or empty.", logPrefix);
                    return null;
                }

                long newId = await _bookingRepository.AddBookingAsync(request, cancellationToken);

                if (newId > 0)
                {
                    _logger.LogInformation("{Prefix} Booking created successfully with ID {Id}", logPrefix, newId);

                    await _hubContext.Clients.All.ReceiveNewBooking(request);

                    return newId;
                }
                else
                {
                    _logger.LogWarning("{Prefix} Failed to create booking for CallId {CallId}. Repository returned 0.", logPrefix, request.CallId);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Exception creating booking for CallId '{CallId}'", logPrefix, request.CallId);
                return null;
            }
        }

        public async Task<PagedList<PaginationBookingDTO>?> GetBookingsPaginatedAsync(PaginationParameters parameters, CancellationToken cancellationToken = default)
        {
            return await _bookingRepository.GetBookingsPaginatedAsync(parameters, cancellationToken);
        }

        public async Task<BookingDTO?> GetBookingByIdAsync(long bookingId, CancellationToken cancellationToken = default)
        {
            return await _bookingRepository.GetBookingByIdAsync(bookingId, cancellationToken);
        }

        public async Task<BookingStatisticsDTO?> GetBookingStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _bookingRepository.GetBookingStatisticsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service: GetBookingStatisticsAsync: Error retrieving statistics");
                return null;
            }
        }

        public async Task<bool> UpdateBookingStatusAsync(long bookingId, enBookingStatus status, CancellationToken cancellationToken = default)
        {
            const string logPrefix = "Service: UpdateBookingStatusAsync:";
            try
            {
                bool result = await _bookingRepository.UpdateBookingStatusAsync(bookingId, status, cancellationToken);

                if (result)
                {
                    _logger.LogInformation("{Prefix} Booking {Id} status updated to {Status}", logPrefix, bookingId, status);

                    var updatedBooking = await _bookingRepository.GetBookingByIdAsync(bookingId, cancellationToken);
                    if (updatedBooking != null)
                    {
                        await _hubContext.Clients.All.ReceiveBookingUpdate(updatedBooking, status.ToString());
                    }
                }
                else
                {
                    _logger.LogWarning("{Prefix} Failed to update booking {Id}", logPrefix, bookingId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Exception updating status for booking {Id}", logPrefix, bookingId);
                return false;
            }
        }


    }
}
