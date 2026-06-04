using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ProflowApp.Data;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ================================================================
// SERVICES
// ================================================================

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// MVC + Anti-CSRF Global
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddAuthorization();

// Session hardening
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.Services.AddMemoryCache();

// Audit Service
builder.Services.AddScoped<ProFlowApp.Services.AuditService>();

// Sembunyikan header Server: Kestrel
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

// Rate Limiter
builder.Services.AddRateLimiter(options =>
{
    // Login policy — PER IP menggunakan AddPolicy
    // BUKAN AddFixedWindowLimiter yang shared semua IP
    options.AddPolicy("login_policy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Setiap IP punya counter SENDIRI
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }
        )
    );

    // Global limiter — semua endpoint per IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }
        )
    );

    options.OnRejected = async (context, token) =>
    {
        Console.WriteLine($"=== RATE LIMIT HIT: {context.HttpContext.Connection.RemoteIpAddress} ===");

        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.Headers["Retry-After"] = "60";
        context.HttpContext.Response.Redirect("/Account/Login?blocked=true");
        await Task.CompletedTask;
    };
});

// builder.WebHost.ConfigureKestrel(o =>
// {
//     o.AddServerHeader = false;
//     o.ListenAnyIP(5025); // ← tambah ini agar bisa diakses via IP jaringan
// });

var app = builder.Build();

// ================================================================
// MIDDLEWARE PIPELINE — urutan sangat penting!
// ================================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");

app.UseHttpsRedirection();

// Rate Limiter — harus setelah UseHttpsRedirection
// agar redirect HTTPS tidak ikut terhitung sebagai request
app.UseRateLimiter();

// Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    // context.Response.Headers.Append("Content-Security-Policy",
    //     "default-src 'self'; " +
    //     "script-src 'self' 'unsafe-inline' cdnjs.cloudflare.com; " +
    //     "style-src 'self' 'unsafe-inline' fonts.googleapis.com; " +
    //     "font-src 'self' fonts.gstatic.com; " +
    //     "img-src 'self' data:;");
    // context.Response.Headers.Append("Permissions-Policy",
    //     "camera=(), microphone=(), geolocation=()");
    // context.Response.Headers.Append("Cross-Origin-Embedder-Policy", "require-corp");
    // context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");
    // context.Response.Headers.Append("Cross-Origin-Resource-Policy", "same-origin");
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();