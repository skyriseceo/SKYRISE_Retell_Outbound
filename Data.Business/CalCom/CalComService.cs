using Data.Access.DTOs;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;


namespace Data.Business.CalCom;

public class CalComService : ICalComService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CalComService> _logger;

    public CalComService(IHttpClientFactory httpClientFactory, ILogger<CalComService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }


    public async Task<PagedList<ExternalBookingViewModel>> GetBookingsAsync(
        PaginationParameters paginationParams,
        CancellationToken cancellationToken = default)
    {
        const string logPrefix = "CalComService: GetBookingsAsync:";
        _logger.LogInformation("{Prefix} Attempting to fetch bookings from Cal.com.", logPrefix);

        try
        {
            var client = _httpClientFactory.CreateClient("CalClient");

            int take = paginationParams.PageSize;
            int skip = (paginationParams.PageNumber - 1) * paginationParams.PageSize;

            // تكوين Query String مع فلترة Search و Status
            var queryParams = new Dictionary<string, string?>
            {
                ["take"] = take.ToString(),
                ["skip"] = skip.ToString()
            };

            if (!string.IsNullOrWhiteSpace(paginationParams.SearchTerm))
            {
                queryParams["search"] = paginationParams.SearchTerm;
            }

            if (paginationParams.Status.HasValue)
            {
                // تحويل القيمة إلى string حسب ما يتوقعه API
                queryParams["status"] = paginationParams.Status.Value.ToString();
            }

            string requestUrl = QueryHelpers.AddQueryString("v2/bookings", queryParams);

            // استدعاء API
            var apiResponse = await client.GetFromJsonAsync<CalApiResponse>(requestUrl, cancellationToken);

            // التحقق من الاستجابة
            if (apiResponse == null || apiResponse.Status != "success" || apiResponse.Data == null)
            {
                _logger.LogWarning("{Prefix} Cal.com API returned no data or non-success status.", logPrefix);
                return PagedList<ExternalBookingViewModel>.Empty<ExternalBookingViewModel>(
                    paginationParams.PageNumber,
                    paginationParams.PageSize
                );
            }

            // تحويل البيانات إلى ViewModel
            var viewModels = apiResponse.Data.Select(booking =>
            {
                var firstAttendee = booking.Attendees?.FirstOrDefault();
                return new ExternalBookingViewModel
                {
                    Customer = firstAttendee?.Name ?? string.Empty,
                    PhoneNumber = firstAttendee?.PhoneNumber ?? string.Empty,
                    Email = firstAttendee?.Email ?? string.Empty,
                    Status = booking.Status,
                    BookingDate = booking.Start,
                    CreatedAt = booking.CreatedAt
                };
            }).ToList();

            // إرجاع PagedList
            return new PagedList<ExternalBookingViewModel>(
                viewModels,
                apiResponse.Pagination.TotalItems,
                apiResponse.Pagination.CurrentPage,
                paginationParams.PageSize
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Prefix} Failed to fetch bookings from Cal.com.", logPrefix);
            return PagedList<ExternalBookingViewModel>.Empty<ExternalBookingViewModel>(
                paginationParams.PageNumber,
                paginationParams.PageSize
            );
        }
    }




}
