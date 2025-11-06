using Dapper;
using Microsoft.Extensions.Logging;
using Data.Access.Connection;
using Data.Access.DTOs;

namespace Data.Access.Booking
{
    public class BookingRepository : IBookingRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<BookingRepository> _logger;

        public BookingRepository(IDbConnectionFactory connectionFactory, ILogger<BookingRepository> logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // (داخل public class BookingRepository)
        public async Task<long> AddBookingAsync(BookingDTO bookingDto, CancellationToken cancellationToken = default)
        {
            const string logPrefix = "BookingRepository: AddBookingAsync:";
            _logger.LogInformation("{Prefix} Attempting to add new booking for call {CallId}", logPrefix, bookingDto.CallId);

            try
            {
                using var _dbConnection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

                // (تأكد أن الدالة تستدعى بـ 7 متغيرات كما في OSV.sql)
                const string sql = @"
                SELECT public.fn_add_booking(
                    @ProspectName, 
                    @ProspectEmail, 
                    @ProspectPhone, 
                    @AppointmentTime, 
                    @AgentId, 
                    @CallId, 
                    @Status
                );";

                var bookingId = await _dbConnection.ExecuteScalarAsync<long>(
                    sql,
                    new
                    {
                        ProspectName = bookingDto.ProspectName,
                        ProspectEmail = bookingDto.ProspectEmail,
                        ProspectPhone = (string.IsNullOrEmpty(bookingDto.ProspectPhone)) ? null : bookingDto.ProspectPhone, // (ضمان إرسال null)
                        AppointmentTime = bookingDto.AppointmentTime,
                        AgentId = bookingDto.AgentId,
                        CallId = bookingDto.CallId,
                        Status = bookingDto.Status 
                    }
                );

                _logger.LogInformation("{Prefix} Successfully added booking. New ID: {Id}", logPrefix, bookingId);
                return bookingId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to add booking for call {CallId}", logPrefix, bookingDto.CallId);
                throw;
            }
        }

        public async Task<PagedList<PaginationBookingDTO>> GetBookingsPaginatedAsync(PaginationParameters parameters, CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT * FROM public.fn_get_bookings_paginated(@p_page_number, @p_page_size, @p_search_term, @p_status);";

            _logger.LogInformation("Executing SQL function: {SqlFunction} with PageNumber: {PageNumber}, PageSize: {PageSize}, Search: {Search}, Status: {Status}",
                "fn_get_bookings_paginated", parameters.PageNumber, parameters.PageSize, parameters.SearchTerm, parameters.Status);

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

                var dapperParameters = new
                {
                    p_page_number = parameters.PageNumber,
                    p_page_size = parameters.PageSize,
                    p_search_term = parameters.SearchTerm,
                    p_status = parameters.Status
                };

                var bookings = await connection.QueryAsync<PaginationBookingDTO>(
                    new CommandDefinition(sql, dapperParameters, cancellationToken: cancellationToken)
                );

                var items = bookings.ToList();
                var totalCount = items.FirstOrDefault()?.TotalCount ?? 0;

                return new PagedList<PaginationBookingDTO>(items, totalCount, parameters.PageNumber, parameters.PageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute SQL function: fn_get_bookings_paginated");
                return new PagedList<PaginationBookingDTO>(new List<PaginationBookingDTO>(), 0, parameters.PageNumber, parameters.PageSize);
            }
        }

        public async Task<BookingDTO?> GetBookingByIdAsync(long bookingId, CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT * FROM public.fn_get_booking_by_id(@p_booking_id);";
            _logger.LogInformation("Executing SQL function: {SqlFunction} for BookingId: {Id}", "fn_get_booking_by_id", bookingId);

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                var parameters = new { p_booking_id = bookingId };
                var booking = await connection.QuerySingleOrDefaultAsync<BookingDTO?>(
                    new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)
                );

                return booking;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute SQL function: fn_get_booking_by_id for ID {Id}", bookingId);
                return null;
            }
        }


        public async Task<BookingStatisticsDTO?> GetBookingStatisticsAsync(CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT * FROM public.fn_get_booking_statistics();";
            _logger.LogInformation("Executing SQL function: {SqlFunction}", "fn_get_booking_statistics");

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                var statistics = await connection.QuerySingleOrDefaultAsync<BookingStatisticsDTO>(
                    new CommandDefinition(sql, cancellationToken: cancellationToken)
                );
                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get booking statistics");
                return null;
            }
        }

        public async Task<bool> UpdateBookingStatusAsync(long bookingId, enBookingStatus status, CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT public.fn_update_booking_status(@p_booking_id, @p_status);";
            _logger.LogInformation("Executing SQL function: {SqlFunction} for Booking ID {Id} to {Status}", "fn_update_booking_status", bookingId, status);

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                var parameters = new
                {
                    p_booking_id = bookingId,
                    p_status = (int)status 
                };

                var result = await connection.ExecuteScalarAsync<bool>(
                    new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)
                );
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute SQL function: fn_update_booking_status for Booking ID {Id}", bookingId);
                return false;
            }
        }

        // (داخل public class BookingRepository)

        public async Task<bool> UpdateBookingStatusByCallIdAsync(string callId, enBookingStatus newStatus, CancellationToken cancellationToken = default)
        {
            const string logPrefix = "BookingRepository: UpdateBookingStatusByCallIdAsync:";
            _logger.LogInformation("{Prefix} Attempting to update status for call {CallId} to {Status}", logPrefix, callId, newStatus);
            try
            {
                using var _dbConnection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

                // (استدعاء الدالة الجديدة التي أنشأناها في SQL)
                const string sql = "SELECT public.fn_update_booking_status_by_call_id(@CallId, @NewStatus);";

                return await _dbConnection.ExecuteScalarAsync<bool>(sql, new { CallId = callId, NewStatus = (int)newStatus });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to update status for call {CallId}", logPrefix, callId);
                throw;
            }
        }

        public async Task<bool> RescheduleBookingByCallIdAsync(string callId, DateTime newStartTime, CancellationToken cancellationToken = default)
        {
            const string logPrefix = "BookingRepository: RescheduleBookingByCallIdAsync:";
            _logger.LogInformation("{Prefix} Attempting to reschedule call {CallId} to {NewTime}", logPrefix, callId, newStartTime);
            try
            {
                using var _dbConnection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

                // (استدعاء الدالة الجديدة التي أنشأناها في SQL)
                const string sql = "SELECT public.fn_reschedule_booking_by_call_id(@CallId, @NewStartTime);";

                return await _dbConnection.ExecuteScalarAsync<bool>(sql, new{ CallId = callId, NewStartTime = newStartTime });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to reschedule call {CallId}", logPrefix, callId);
                throw;
            }
        }

        public async Task<BookingDTO?> GetBookingByCallIdAsync(string callId, CancellationToken cancellationToken = default)
        {
            const string logPrefix = "BookingRepository: GetBookingByCallIdAsync:";
            const string sql = "SELECT * FROM public.fn_get_booking_by_call_id(@CallId);";
            _logger.LogInformation("{Prefix} Getting booking by CallId {CallId}", logPrefix, callId);
            try
            {
                using var _dbConnection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

                var booking = await _dbConnection.QuerySingleOrDefaultAsync<BookingDTO>(sql, new { CallId = callId });
                return booking ?? null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Failed to get booking by CallId {CallId}", logPrefix, callId);
                throw;
            }
        }
    }
}