using Ecomm.Data;
using Ecomm.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// ✅ CHANGED: Switch from SQLite to PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity configuration
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 3;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

// ✅ ADD THIS LINE: Register Razor Pages with Areas support
builder.Services.AddRazorPages();

// Add User Registration Service
builder.Services.AddScoped<UserRegistrationService>();

var app = builder.Build();

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

            // Optional: Count products if you have that table
            // var productCount = context.Products?.Count() ?? 0;
            // Console.WriteLine($"📊 Found {productCount} products in database.");
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
                await userManager.AddToRoleAsync(firstUser, "Admin");
                Console.WriteLine($"✅ First user '{firstUser.Email}' automatically assigned Admin role!");
            }
            else
            {
                Console.WriteLine($"✅ Admin users exist: {string.Join(", ", adminUsers.Select(u => u.Email))}");
            }
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ An error occurred setting up admin role.");
    }
}

app.Run();