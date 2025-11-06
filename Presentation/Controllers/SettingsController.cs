using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OSV.Models;
using System.Text.Json.Serialization;

namespace OSV.Controllers
{
    [Authorize]
    [Route("api/settings")]
    public class SettingsController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public SettingsController(
     UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet("/Settings/Index")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            return View();
        }

        // GET: Current User Profile
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                id = user.Id,
                userName = user.UserName,
                email = user.Email,
                emailConfirmed = user.EmailConfirmed,
                phoneNumber = user.PhoneNumber,
                roles = roles
            });
        }

        // PUT: Update Profile
        [HttpPut("Updateprofilee")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            // Validate Email format if provided
            if (!string.IsNullOrEmpty(request.email))
            {
                var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                if (!emailRegex.IsMatch(request.email))
                {
                    return BadRequest(new { message = "Invalid email format." });
                }
            }

            // Validate phoneNumber number format if provided (basic validation)
            if (!string.IsNullOrEmpty(request.phoneNumber))
            {
                var phoneRegex = new System.Text.RegularExpressions.Regex(@"^\+?[\d\s\-\(\)]+$");
                if (!phoneRegex.IsMatch(request.phoneNumber))
                {
                    return BadRequest(new { message = "Invalid PhoneNumber number format." });
                }
            }

            // Update Email
            if (!string.IsNullOrEmpty(request.email) && request.email != user.Email)
            {
                var emailResult = await _userManager.SetEmailAsync(user, request.email);
                if (!emailResult.Succeeded)
                {
                    var errors = emailResult.Errors.Select(e => e.Description);
                    return BadRequest(new { message = "Failed to update email", errors });
                }
            }

            // Update username
            if (!string.IsNullOrEmpty(request.userName) && request.userName != user.UserName)
            {
                var usernameResult = await _userManager.SetUserNameAsync(user, request.userName);
                if (!usernameResult.Succeeded)
                {
                    var errors = usernameResult.Errors.Select(e => e.Description);
                    return BadRequest(new { message = "Failed to update username", errors });
                }
            }

            // Update phoneNumber number
            if (request.phoneNumber != user.PhoneNumber)
            {
                var phoneResult = await _userManager.SetPhoneNumberAsync(user, request.phoneNumber);
                if (!phoneResult.Succeeded)
                {
                    var errors = phoneResult.Errors.Select(e => e.Description);
                    return BadRequest(new { message = "Failed to update PhoneNumber number", errors });
                }
            }

            return Ok(new { message = "Profile updated successfully" });
        }

        // PUT: Change Password
        [HttpPut("password")]
        [ProducesResponseType(typeof(ApiResponse), 200)]
        [ProducesResponseType(typeof(ApiResponse), 400)]
        [ProducesResponseType(typeof(ApiResponse), 404)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            // Validate confirmPassword matches newPassword
            if (request.newPassword != request.confirmPassword)
            {
                return BadRequest(new { message = "New password and confirmation password do not match." });
            }

            // Validate password strength (minimum requirements)
            if (request.newPassword.Length < 8)
            {
                return BadRequest(new { message = "Password must be at least 8 characters long." });
            }

            IdentityResult result;
            
            // Check if user has a password
            var hasPassword = await _userManager.HasPasswordAsync(user);
            
            if (!hasPassword)
            {
                // User doesn't have a password (e.g., registered via external login)
                // Use AddPasswordAsync instead of ChangePasswordAsync
                result = await _userManager.AddPasswordAsync(user, request.newPassword);
            }
            else
            {
                // User has a password, use ChangePasswordAsync
                result = await _userManager.ChangePasswordAsync(
                    user,
                    request.currentPassword,
                    request.newPassword
                );
            }

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                return BadRequest(new { message = "Failed to change password", errors });
            }

            await _signInManager.RefreshSignInAsync(user);

            return Ok(new { message = "Password changed successfully" });
        }
    }

    public class UpdateProfileRequest
    {
        [JsonPropertyName("userName")]
        public string? userName { get; set; }

        [JsonPropertyName("email")]
        public string? email { get; set; }

        [JsonPropertyName("phoneNumber")]
        public string? phoneNumber { get; set; }
    }

    public class ChangePasswordRequest
    {
        [JsonPropertyName("currentPassword")]
        public string currentPassword { get; set; } = string.Empty;

        [JsonPropertyName("newPassword")]
        public string newPassword { get; set; } = string.Empty;

        [JsonPropertyName("confirmPassword")]
        public string confirmPassword { get; set; } = string.Empty;
    }
}
