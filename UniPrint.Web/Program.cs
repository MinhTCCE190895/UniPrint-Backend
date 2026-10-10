using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Context;
using UniPrint.DataAccess.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<UniPrintDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// 2. Dependency Injection for Business Services
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IPriceCalculatorService, PriceCalculatorService>();
builder.Services.AddScoped<IPrintOrderService, PrintOrderService>();
builder.Services.AddScoped<IStudyHubService, StudyHubService>();

// 3. Cookie Authentication for Razor Pages
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
    });

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Student", "StudentOnly");
    options.Conventions.AuthorizeFolder("/Staff", "StaffOnly");
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StudentOnly", policy => policy.RequireRole("Student"));
    options.AddPolicy("StaffOnly", policy => policy.RequireRole("Staff", "Admin"));
});

var app = builder.Build();

// Tự động khởi tạo Database và Seed Data nếu chưa có
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UniPrintDbContext>();
    dbContext.Database.EnsureCreated();

    // Tự động bơm đơn in mẫu nếu database chưa có đơn nào
    var studentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    if (!dbContext.PrintOrders.Any())
    {
        var doc1 = new UniPrint.DataAccess.Entities.Document
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888801"),
            UserId = studentId,
            FileName = "BaoCao_DoAn_TotNghiep.pdf",
            FilePath = "/uploads/sample_baocao.pdf",
            FileExtension = ".pdf",
            FileSizeBytes = 2048576,
            PageCount = 35
        };

        var doc2 = new UniPrint.DataAccess.Entities.Document
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888802"),
            UserId = studentId,
            FileName = "Slide_ThuyetTrinh_PRN231.pdf",
            FilePath = "/uploads/sample_slide.pdf",
            FileExtension = ".pdf",
            FileSizeBytes = 1048576,
            PageCount = 20
        };

        var opt1 = new UniPrint.DataAccess.Entities.PrintOption
        {
            Id = Guid.Parse("99999999-9999-9999-9999-999999999901"),
            NumberOfCopies = 2,
            IsColor = false,
            IsDoubleSided = true,
            HasBinding = true,
            HasStaple = false,
            UnitPricePerPage = 315m,
            TotalPrice = 27050m
        };

        var opt2 = new UniPrint.DataAccess.Entities.PrintOption
        {
            Id = Guid.Parse("99999999-9999-9999-9999-999999999902"),
            NumberOfCopies = 1,
            IsColor = true,
            IsDoubleSided = false,
            HasBinding = false,
            HasStaple = true,
            UnitPricePerPage = 1500m,
            TotalPrice = 31000m
        };

        dbContext.Documents.AddRange(doc1, doc2);
        dbContext.PrintOptions.AddRange(opt1, opt2);

        var order1 = new UniPrint.DataAccess.Entities.PrintOrder
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"),
            OrderCode = "ORD-20261001-001",
            StudentId = studentId,
            DocumentId = doc1.Id,
            PrintOption = opt1,
            Status = UniPrint.DataAccess.Enums.PrintOrderStatus.Pending,
            Notes = "In gấp sáng nay giúp em ạ",
            CreatedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        var order2 = new UniPrint.DataAccess.Entities.PrintOrder
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02"),
            OrderCode = "ORD-20261002-002",
            StudentId = studentId,
            DocumentId = doc2.Id,
            PrintOption = opt2,
            Status = UniPrint.DataAccess.Enums.PrintOrderStatus.Processing,
            Notes = "Dập ghim góc trái",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        dbContext.PrintOrders.AddRange(order1, order2);
        dbContext.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
