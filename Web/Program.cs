using Core.Services;
using Core.Services.Email;
using Domain.Models;
using Infrastructure;
using Infrastructure.Data;
using Infrastructure.Interfaces;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using static System.Net.WebRequestMethods;

var builder = WebApplication.CreateBuilder(args);

// File upload configuration
//builder.WebHost.ConfigureKestrel(options =>
//{
//    options.Limits.MaxRequestBodySize = 52428800; // 50 MB
//});

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews();

// postgres Database
//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

//AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// SQL Server Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<DiscountService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<ShippingService>();
builder.Services.AddScoped<AdminOrderService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<HomeMediaService>();
//// Add Paymob HTTP client
//builder.Services.AddHttpClient("Paymob", client =>
//{
//    client.BaseAddress = new Uri("https://accept.paymob.com/api/");
//    client.DefaultRequestHeaders.Accept.Add(
//        new MediaTypeWithQualityHeaderValue("application/json"));
//});
// Add HTTP client for Paymob
builder.Services.AddHttpClient("Paymob", client =>
{
    client.BaseAddress = new Uri("https://accept.paymobsolutions.com/api/");
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
// In Program.cs, add this after builder.Services.AddMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<DistributedCacheService>(); // Add this line
// Add configuration access
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);


// Add session support (for guest cart)
builder.Services.AddDistributedMemoryCache(); // Use Redis in production
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7); // Match cookie expiry
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Add background service
builder.Services.AddHostedService<CartCleanupService>();

// Add AutoMapper if not already added
builder.Services.AddAutoMapper(typeof(Program));

// Identity
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Cookie Authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.LoginPath = "/admin/auth/login";
    options.LogoutPath = "/admin/auth/logout";
    options.AccessDeniedPath = "/admin/auth/access-denied";
});

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// Enhanced Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);


var app = builder.Build();

// CRITICAL: Enhanced error handling
//app.UseExceptionHandler(errorApp =>
//{
//    errorApp.Run(async context =>
//    {
//        var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
//        var exception = exceptionHandlerPathFeature?.Error;

//        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
//        logger.LogError(exception, "💥 UNHANDLED EXCEPTION: {Message}", exception?.Message);
//        logger.LogError("💥 Stack Trace: {StackTrace}", exception?.StackTrace);
//        logger.LogError("💥 Path: {Path}", context.Request.Path);

//        context.Response.StatusCode = 500;
//        await context.Response.WriteAsync($"Error: {exception?.Message}");
//    });
//});
//Configure the HTTP request pipeline.

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
else
{
    app.UseDeveloperExceptionPage(); // Enable detailed errors in development
}

//app.UseDeveloperExceptionPage(); // Shows detailed errors in development

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession(); // ← ADD THIS LINE - CRITICAL FOR SESSION ID

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Auth}/{action=Login}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Database initialization with better error handling
using (var scope = app.Services.CreateScope())
{
    try
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("🔧 Initializing database...");

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await DbInitializer.Initialize(db, userManager, roleManager);

        logger.LogInformation("✅ Database initialized successfully");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ Error during database initialization");
        throw; // Re-throw to prevent app from starting with bad state
    }
}

Console.WriteLine("🚀 Application started successfully!");
app.Run();