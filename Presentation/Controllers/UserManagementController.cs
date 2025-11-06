using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace OSV.Controllers
{
    [Authorize(Roles = "Admin")] 
    public class UserManagementController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<UserManagementController> _logger;

        public UserManagementController(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<UserManagementController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        [Route("api/users/all")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = _userManager.Users.ToList();
                var userList = new List<object>();

                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    userList.Add(new
                    {
                        id = user.Id,
                        email = user.Email,
                        userName = user.UserName,
                        phoneNumber = user.PhoneNumber,
                        emailConfirmed = user.EmailConfirmed,
                        role = roles.FirstOrDefault() ?? "User",
                        roles = roles,
                        createdAt = DateTime.UtcNow // Placeholder since IdentityUser doesn't have Created_At
                    });
                }

                return Ok(userList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve users");
                return StatusCode(500, new { success = false, message = "Failed to retrieve users" });
            }
        }

        [HttpGet]
        [Route("api/users/{id}")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GetUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                var roles = await _userManager.GetRolesAsync(user);
                
                return Ok(new
                {
                    id = user.Id,
                    userName = user.UserName,
                    email = user.Email,
                    phoneNumber = user.PhoneNumber,
                    emailConfirmed = user.EmailConfirmed,
                    role = roles.FirstOrDefault() ?? "User",
                    roles = roles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user");
                return StatusCode(500, new { success = false, message = "Failed to get user" });
            }
        }

        [HttpPost]
        [Route("api/users/create")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                var user = new IdentityUser
                {
                    UserName = request.UserName ?? request.Email,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, request.Password);

                if (result.Succeeded)
                {
                    if (!string.IsNullOrEmpty(request.Role))
                    {
                        if (!await _roleManager.RoleExistsAsync(request.Role))
                        {
                            await _roleManager.CreateAsync(new IdentityRole(request.Role));
                        }

                        await _userManager.AddToRoleAsync(user, request.Role);
                    }
                    else
                    {
                        // Default role is "User"
                        if (!await _roleManager.RoleExistsAsync("User"))
                        {
                            await _roleManager.CreateAsync(new IdentityRole("User"));
                        }
                        await _userManager.AddToRoleAsync(user, "User");
                    }

                    _logger.LogInformation("User {email} created successfully", request.Email);
                    return Ok(new { success = true, message = "User created successfully", userId = user.Id });
                }

                return BadRequest(new
                {
                    success = false,
                    message = string.Join(", ", result.Errors.Select(e => e.Description))
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create user");
                return StatusCode(500, new { success = false, message = "Failed to create user" });
            }
        }

        [HttpPut]
        [Route("api/users/update/{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                // Update basic info
                user.UserName = request.UserName ?? user.UserName;
                user.Email = request.Email ?? user.Email;
                user.PhoneNumber = request.PhoneNumber;

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    return BadRequest(new { success = false, message = "Failed to update user" });
                }

                // Update roles if provided
                if (!string.IsNullOrEmpty(request.Role))
                {
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);

                    if (!await _roleManager.RoleExistsAsync(request.Role))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(request.Role));
                    }

                    await _userManager.AddToRoleAsync(user, request.Role);
                }

                _logger.LogInformation("User {email} updated successfully", user.Email);
                return Ok(new { success = true, message = "User updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user");
                return StatusCode(500, new { success = false, message = "Failed to update user" });
            }
        }

        [HttpDelete]
        [Route("api/users/delete/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                // Prevent deleting yourself
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.Id == id)
                {
                    return BadRequest(new { success = false, message = "You cannot delete your own account" });
                }

                // Check if user is Admin
                var userRoles = await _userManager.GetRolesAsync(user);
                if (userRoles.Contains("Admin"))
                {
                    // Count total admins
                    var admins = await _userManager.GetUsersInRoleAsync("Admin");
                    if (admins.Count <= 1)
                    {
                        return BadRequest(new { success = false, message = "Cannot delete the last administrator in the system" });
                    }
                }

                var result = await _userManager.DeleteAsync(user);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User {email} deleted successfully", user.Email);
                    return Ok(new { success = true, message = "User deleted successfully" });
                }

                return BadRequest(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete user");
                return StatusCode(500, new { success = false, message = "Failed to delete user" });
            }
        }
    }

    public class CreateUserRequest
    {
        public string? UserName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = "User";
    }

    public class UpdateUserRequest
    {
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Role { get; set; }
    }
}
