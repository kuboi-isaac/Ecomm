using Ecomm.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace Ecomm.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly UserRegistrationService _userRegistrationService;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        public RegisterModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            UserRegistrationService userRegistrationService,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _userRegistrationService = userRegistrationService;
            _logger = logger;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Invalid email address")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Password is required")]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Display(Name = "First Name")]
            [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
            public string FirstName { get; set; }

            [Display(Name = "Last Name")]
            [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
            public string LastName { get; set; }

            [Display(Name = "Phone Number")]
            [Phone(ErrorMessage = "Invalid phone number")]
            public string PhoneNumber { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                var user = new IdentityUser
                {
                    UserName = Input.Email,
                    Email = Input.Email,
                    PhoneNumber = Input.PhoneNumber
                };

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    // Handle automatic admin assignment for first user
                    await _userRegistrationService.HandleNewUserRegistration(user);

                    // Add custom claims if needed
                    if (!string.IsNullOrEmpty(Input.FirstName))
                    {
                        await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("FirstName", Input.FirstName));
                    }
                    if (!string.IsNullOrEmpty(Input.LastName))
                    {
                        await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("LastName", Input.LastName));
                    }

                    // Send confirmation email (optional)
                    // var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    // await _emailSender.SendEmailAsync(Input.Email, "Confirm your email", code);

                    await _signInManager.SignInAsync(user, isPersistent: false);

                    TempData["Success"] = "Account created successfully! Welcome to our store.";
                    return LocalRedirect(returnUrl);
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}