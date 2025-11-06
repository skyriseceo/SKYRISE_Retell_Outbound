using Data.Access.DTOs;
using FluentValidation;
using static Data.Business.Data.Requests;

namespace Data.Business.Service
{
  
 
    public class CustomerValidator : AbstractValidator<CustomerDTO>
    {
        public CustomerValidator()
        {
            RuleFor(c => c)
                .NotNull()
                .WithMessage("Customer object cannot be null.");

            RuleFor(c => c.Id)
                .GreaterThan(0)
                .WithMessage("Customer Id must be greater than zero.");

            RuleFor(c => c.Name)
                .NotEmpty()
                .WithMessage("Customer Name is required.")
                .MaximumLength(100)
                .WithMessage("Customer Name must not exceed 100 characters.");

            
           RuleFor(c => c.PhoneNumber)
               .NotEmpty()
               .WithMessage("Phone number is required.");
           

           RuleFor(c => c.Status)
               .IsInEnum()
               .WithMessage("Invalid status value.");
        }
    }

    /// <summary>
    /// (مُعدل) - للتحقق من بيانات الـ AI (في الـ Function Call)
    /// </summary>
    public class BookingToolRequestValidator : AbstractValidator<BookingToolRequest>
    {
        public BookingToolRequestValidator()
        {
            RuleFor(x => x.CustomerName)
                .NotEmpty()
                .WithMessage("customer_name is required.");

            RuleFor(x => x.Datetime)
                .NotEmpty()
                .WithMessage("datetime is required.")
                .Must(dt => dt != DateTime.MinValue && dt > DateTime.UtcNow.AddMinutes(-5))
                .WithMessage("Appointment time must be in the (near) future.");

            RuleFor(x => x.Email)
                .EmailAddress()
                .WithMessage("A valid email address is required.")
                .When(x => !string.IsNullOrEmpty(x.Email));
        }
    }


    public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
    {
        public CreateCustomerRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Name is required.")
                .MaximumLength(100)
                .WithMessage("Name must not exceed 100 characters.");

            RuleFor(x => x.phoneNumber)
                .NotEmpty()
                .WithMessage("Phone number is required.");

            RuleFor(x => x.Email)
                 .EmailAddress()
                 .WithMessage("A valid email address is required.")
                 .When(x => !string.IsNullOrEmpty(x.Email));
        }
    }
}