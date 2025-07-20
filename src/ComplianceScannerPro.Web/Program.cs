using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Infrastructure.Data;
using ComplianceScannerPro.Infrastructure.Repositories;
using ComplianceScannerPro.Infrastructure.Identity;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();

builder.Host.UseSerilog();

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=compliancescannerdb;Username=scanuser;Password=SecurePass123!";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Identity configuration
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Repository pattern
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Business services
builder.Services.AddHttpClient<IWebCrawlerService, ComplianceScannerPro.Infrastructure.Services.WebCrawlerService>();
builder.Services.AddScoped<IAccessibilityAnalyzer, ComplianceScannerPro.Infrastructure.Services.AccessibilityAnalyzer>();
builder.Services.AddScoped<IReportGenerator, ComplianceScannerPro.Infrastructure.Services.SimpleReportGenerator>();
builder.Services.AddScoped<IScanService, ComplianceScannerPro.Infrastructure.Services.ScanService>();
builder.Services.AddScoped<ISubscriptionService, ComplianceScannerPro.Infrastructure.Services.SubscriptionService>();

// Add controllers and API support
builder.Services.AddControllers();
builder.Services.AddRazorPages();

// Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "ComplianceScannerPro API", 
        Version = "v1",
        Description = "API pour l'audit d'accessibilité RGAA"
    });
});

// Rate limiting sera implémenté via middleware custom plus tard

// CORS for API
builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ComplianceScannerPro API v1"));
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("ApiPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

// Database migration on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        await context.Database.MigrateAsync();
        Log.Information("Database migration completed successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while migrating the database");
    }
}

Log.Information("ComplianceScannerPro started successfully");
app.Run();
