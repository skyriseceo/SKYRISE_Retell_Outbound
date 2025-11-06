using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace OSV.Models
{
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object? Data { get; set; }
        public IEnumerable<string> Errors { get; set; }

        public ApiResponse(bool success, string message, object? data = null)
        {
            Success = success;
            Message = message;
            Data = data;
            Errors = new List<string>();
        }

        public ApiResponse(ModelStateDictionary modelState)
        {
            Success = false;
            Message = "Validation failed.";
            Errors = modelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage);
            Data = null;
        }
    }

    public class ApiResponse<T> : ApiResponse
    {
        public new T Data { get; set; } = default!;

        public ApiResponse(bool success, string message, T data)
            : base(success, message)
        {
            Data = data;
        }
    }
}
