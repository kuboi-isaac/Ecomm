using Ecomm.Data;
using Ecomm.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// ✅ CHANGED: Switch from SQLite to PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ ADDED: Session support for guest cart functionality
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Guest cart persists for 1 day
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // Mark as essential for GDPR
    options.Cookie.Name = ".Ecomm.Session";
});


// Identity configuration with better security settings
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true; // Changed to true for email confirmation
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
    options.Lockout.MaxFailedAccessAttempts = 3;

    // User settings
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

// ✅ ADD THIS LINE: Register Razor Pages with Areas support
builder.Services.AddRazorPages();

// Add User Registration Service
builder.Services.AddScoped<UserRegistrationService>();

// Configure Email Settings from appsettings.json and register as singleton
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddSingleton<EmailSettings>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    return config.GetSection("EmailSettings").Get<EmailSettings>() ?? new EmailSettings();
});

// Add Email Sender Service with proper dependency injection
//builder.Services.AddTransient<IEmailSender, EmailSender>();

// ✅ ADDED: Uganda Shillings localization
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var ugandaCulture = new CultureInfo("en-UG");

    options.DefaultRequestCulture = new RequestCulture(ugandaCulture);
    options.SupportedCultures = new List<CultureInfo> { ugandaCulture };
    options.SupportedUICultures = new List<CultureInfo> { ugandaCulture };
});

var app = builder.Build();

// ✅ Auto-create database tables (including Orders and OrderItems)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // This will create all tables that don't exist
        context.Database.EnsureCreated();
        Console.WriteLine("✅ All database tables created successfully!");

        // Test if orders table works
        var orderCount = context.Orders.Count();
        Console.WriteLine($"📊 Orders table ready. Current orders: {orderCount}");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ An error occurred while creating database tables.");
        Console.WriteLine($"❌ Database creation error: {ex.Message}");
    }
}

// ✅ ADDED: Use localization AFTER building the app
app.UseRequestLocalization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();



// ✅ ADDED: Use session middleware (must be after UseStaticFiles and before UseRouting)
app.UseSession();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ✅ ADD THIS: Map Area routes BEFORE default routes
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Routes - Use ProductsController for all product browsing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ✅ ADD THIS: Map Razor Pages (this should already be there)
app.MapRazorPages();

// ✅ CHANGED: Better database initialization for PostgreSQL
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // For PostgreSQL, use migrations instead of EnsureCreated
        context.Database.Migrate(); // This applies any pending migrations

        Console.WriteLine("✅ PostgreSQL database migrated successfully!");

        // Test the connection with a simple query
        var canConnect = context.Database.CanConnect();
        if (canConnect)
        {
            Console.WriteLine("✅ Successfully connected to PostgreSQL!");

            // Optional: Count users if you have that table
            var userCount = context.Users?.Count() ?? 0;
            Console.WriteLine($"📊 Found {userCount} users in database.");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ An error occurred while setting up the database.");
        Console.WriteLine($"❌ Database error: {ex.Message}");
    }
}

// Create admin role and auto-assign first user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        // Create Admin role if it doesn't exist
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            Console.WriteLine("✅ Admin role created!");
        }

        // Create Customer role if it doesn't exist
        if (!await roleManager.RoleExistsAsync("Customer"))
        {
            await roleManager.CreateAsync(new IdentityRole("Customer"));
            Console.WriteLine("✅ Customer role created!");
        }

        // Check if any users exist
        var existingUsers = await userManager.Users.ToListAsync();

        if (!existingUsers.Any())
        {
            Console.WriteLine("👤 No users found. First registered user will become Admin.");
        }
        else
        {
            // Check if any admin exists
            var adminUsers = await userManager.GetUsersInRoleAsync("Admin");
            if (!adminUsers.Any())
            {
                // Make the first user admin
                var firstUser = existingUsers.First();
                var result = await userManager.AddToRoleAsync(firstUser, "Admin");
                if (result.Succeeded)
                {
                    Console.WriteLine($"✅ First user '{firstUser.Email}' automatically assigned Admin role!");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to assign Admin role to first user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                Console.WriteLine($"✅ Admin users exist: {string.Join(", ", adminUsers.Select(u => u.Email))}");
            }

            // Assign Customer role to non-admin users
            var nonAdminUsers = existingUsers.Where(u => !adminUsers.Contains(u)).ToList();
            foreach (var user in nonAdminUsers)
            {
                if (!await userManager.IsInRoleAsync(user, "Customer"))
                {
                    await userManager.AddToRoleAsync(user, "Customer");
                }
            }
            if (nonAdminUsers.Any())
            {
                Console.WriteLine($"✅ Assigned Customer role to {nonAdminUsers.Count} users.");
            }
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ An error occurred setting up admin role.");
        Console.WriteLine($"❌ Role setup error: {ex.Message}");
    }
}

app.Run();

// Email Settings Configuration Class
public class EmailSettings
{
    public string SmtpServer { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Ecomm Store";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}