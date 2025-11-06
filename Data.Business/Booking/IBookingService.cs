using Data.Access.DTOs;


namespace Data.Business.Booking
{
    public interface IBookingService
    {
        Task<long?> CreateBookingAsync(BookingDTO request, CancellationToken cancellationToken = default);
        Task<PagedList<PaginationBookingDTO>?> GetBookingsPaginatedAsync(PaginationParameters parameters, CancellationToken cancellationToken = default);
        Task<BookingDTO?> GetBookingByIdAsync(long bookingId, CancellationToken cancellationToken = default);
        Task<BookingStatisticsDTO?> GetBookingStatisticsAsync(CancellationToken cancellationToken = default);
        Task<bool> UpdateBookingStatusAsync(long bookingId, enBookingStatus status, CancellationToken cancellationToken = default);
    }
}
