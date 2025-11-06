using Data.Access.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Data.Access.Booking
{
    public interface IBookingRepository
    {
        Task<long> AddBookingAsync(BookingDTO bookingDto, CancellationToken cancellationToken = default);

        Task<PagedList<PaginationBookingDTO>> GetBookingsPaginatedAsync(PaginationParameters parameters, CancellationToken cancellationToken = default);

        Task<BookingDTO?> GetBookingByIdAsync(long bookingId, CancellationToken cancellationToken = default);
        Task<BookingStatisticsDTO?> GetBookingStatisticsAsync(CancellationToken cancellationToken = default);
        Task<bool> UpdateBookingStatusByCallIdAsync(string callId, enBookingStatus newStatus, CancellationToken cancellationToken = default);
        Task<bool> RescheduleBookingByCallIdAsync(string callId, DateTime newStartTime, CancellationToken cancellationToken = default);
        Task<bool> UpdateBookingStatusAsync(long bookingId, enBookingStatus status, CancellationToken cancellationToken = default);

        Task<BookingDTO?> GetBookingByCallIdAsync(string callId, CancellationToken cancellationToken = default);
    }
}