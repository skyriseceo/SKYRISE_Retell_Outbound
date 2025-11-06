using Data.Access.DTOs;


namespace Data.Business.CalCom
{
    public interface ICalComService
    {
        Task<PagedList<ExternalBookingViewModel>> GetBookingsAsync(
        PaginationParameters paginationParams,
        CancellationToken cancellationToken = default
    );
    }
}
