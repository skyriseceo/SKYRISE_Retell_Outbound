using Data.Access.DTOs;


namespace Data.Business.Service.Hubs
{
    public interface IHubs
    {
        Task ReceiveNewCustomer(CustomerDTO newCustomer);
        Task ReceiveCustomerUpdate(CustomerDTO updatedCustomer);
        Task ReceiveCustomerDeletion(long deletedCustomerId);

        Task ReceiveNewBooking(BookingDTO bookingDto);

        Task ReceiveBookingUpdate(BookingDTO updatedBooking, string status);

        Task ReceiveCustomersImported();
    }
}
