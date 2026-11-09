using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using UniPrint.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// 1. HTTP CLIENT → WebApiClient (gọi UniPrint.API)
// =====================================================

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<WebApiClient>();

// =====================================================
// 2. SESSION CONFIGURATION
// (Lưu JWT Token sau khi Login từ API)
// =====================================================

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// =====================================================
// 3. FILE UPLOAD CONFIGURATION
// =====================================================

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
});

// =====================================================
// 4. COOKIE AUTHENTICATION (Server-side session)
// =====================================================

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
    });

// =====================================================
// 5. AUTHORIZATION POLICIES
// =====================================================

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StudentOnly", policy => policy.RequireRole("Student"));
    options.AddPolicy("StaffOnly",   policy => policy.RequireRole("Staff", "Admin"));
    options.AddPolicy("AdminOnly",   policy => policy.RequireRole("Admin"));
});

// =====================================================
// 6. RAZOR PAGES
// =====================================================

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Student", "StudentOnly");
    options.Conventions.AuthorizeFolder("/Staff",   "StaffOnly");
    options.Conventions.AuthorizeFolder("/Admin",   "AdminOnly");
    options.Conventions.AuthorizePage("/StudyHub/Upload",        "StudentOnly");
    options.Conventions.AuthorizePage("/Admin/StudyMaterials",   "AdminOnly");
});

var app = builder.Build();

// =====================================================
// 7. HTTP REQUEST PIPELINE
// =====================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
