using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecomm.Services
{
    public class UserRegistrationService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<UserRegistrationService> _logger;

        public UserRegistrationService(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<UserRegistrationService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        public async Task HandleNewUserRegistration(IdentityUser user)
        {
            try
            {
                // Check if this is the first user in the system
                var totalUsers = await _userManager.Users.CountAsync();

                if (totalUsers == 1) // This user is the first one
                {
                    // Ensure Admin role exists
                    if (!await _roleManager.RoleExistsAsync("Admin"))
                    {
                        await _roleManager.CreateAsync(new IdentityRole("Admin"));
                        _logger.LogInformation("Admin role created automatically.");
                    }

                    // Assign Admin role to first user
                    var result = await _userManager.AddToRoleAsync(user, "Admin");

                    if (result.Succeeded)
                    {
                        _logger.LogInformation($"First user '{user.Email}' automatically assigned Admin role.");

                        // Also confirm their email automatically
                        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        await _userManager.ConfirmEmailAsync(user, token);
                        _logger.LogInformation($"First user '{user.Email}' email confirmed automatically.");
                    }
                    else
                    {
                        _logger.LogError($"Failed to assign Admin role to first user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    }
                }
                else
                {
                    _logger.LogInformation($"User '{user.Email}' registered as regular user. Total users: {totalUsers}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in user registration service");
            }
        }
    }
}