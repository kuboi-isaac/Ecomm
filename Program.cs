using Microsoft.EntityFrameworkCore;
using Ecomm.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=ecomm.db"));

// Remove or comment out these lines if they cause errors:
// builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity configuration - MUST be before builder.Build()
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 3;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

var app = builder.Build(); // Everything after this line is middleware configuration

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Remove or comment out if it causes errors:
    // app.UseMigrationsEndPoint();

    // Use developer exception page instead
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Automatic database creation
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        bool created = context.Database.EnsureCreated();

        if (created)
        {
            Console.WriteLine("✅ Database and tables created successfully!");
        }
        else
        {
            Console.WriteLine("ℹ️ Database already exists.");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ An error occurred while creating the database.");
    }
}

// After database creation, add this:
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated();

        // Test database connection
        var productCount = context.Products.Count();
        Console.WriteLine($"✅ Database connected! Found {productCount} products.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database connection failed: {ex.Message}");
    }
}

app.Run();