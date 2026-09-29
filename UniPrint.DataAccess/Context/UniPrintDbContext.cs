using Microsoft.EntityFrameworkCore;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;

namespace UniPrint.DataAccess.Context;

public class UniPrintDbContext : DbContext
{
    public UniPrintDbContext(DbContextOptions<UniPrintDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<PriceConfig> PriceConfigs => Set<PriceConfig>();
    public DbSet<PrintOption> PrintOptions => Set<PrintOption>();
    public DbSet<Shelf> Shelves => Set<Shelf>();
    public DbSet<PrintOrder> PrintOrders => Set<PrintOrder>();
    public DbSet<PickupQR> PickupQRs => Set<PickupQR>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<StudyMaterial> StudyMaterials => Set<StudyMaterial>();
    public DbSet<MaterialReview> MaterialReviews => Set<MaterialReview>();
    public DbSet<MaterialReport> MaterialReports => Set<MaterialReport>();
    public DbSet<Favorite> Favorites => Set<Favorite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(150);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(150);
        });

        // Configure PrintOrder
        modelBuilder.Entity<PrintOrder>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.HasIndex(o => o.OrderCode).IsUnique();

            entity.HasOne(o => o.Student)
                  .WithMany(u => u.StudentOrders)
                  .HasForeignKey(o => o.StudentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Staff)
                  .WithMany(u => u.ProcessedOrders)
                  .HasForeignKey(o => o.StaffId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Document)
                  .WithMany(d => d.PrintOrders)
                  .HasForeignKey(o => o.DocumentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Shelf)
                  .WithMany(s => s.Orders)
                  .HasForeignKey(o => o.ShelfId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(o => o.PickupQR)
                  .WithOne(q => q.Order)
                  .HasForeignKey<PickupQR>(q => q.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(o => o.Payment)
                  .WithOne(p => p.Order)
                  .HasForeignKey<Payment>(p => p.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure StudyMaterial
        modelBuilder.Entity<StudyMaterial>(entity =>
        {
            entity.HasKey(m => m.Id);

            entity.HasOne(m => m.UploadedByStudent)
                  .WithMany()
                  .HasForeignKey(m => m.UploadedByStudentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Subject)
                  .WithMany(s => s.Materials)
                  .HasForeignKey(m => m.SubjectId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Category)
                  .WithMany(c => c.Materials)
                  .HasForeignKey(m => m.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Favorite (Unique constraint student + material)
        modelBuilder.Entity<Favorite>()
            .HasIndex(f => new { f.StudentId, f.MaterialId })
            .IsUnique();

        // Configure MaterialReview (Unique constraint 1 review per student per material)
        modelBuilder.Entity<MaterialReview>()
            .HasIndex(r => new { r.StudentId, r.MaterialId })
            .IsUnique();

        // Seed initial data
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var staffId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var studentId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        // Password hash for '123456'
        const string defaultHash = "$2a$11$wK.1P0eXoXJ8b7rIvdqjB.ZkmrYxPfvU6zRlh7q8yXqj8wU2s1l8G";

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = adminId,
                FullName = "System Administrator",
                Email = "admin@uniprint.edu.vn",
                PasswordHash = defaultHash,
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = staffId,
                FullName = "Printing Staff 01",
                Email = "staff@uniprint.edu.vn",
                PasswordHash = defaultHash,
                Role = UserRole.Staff,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = studentId,
                FullName = "Nguyen Van Sinh Vien",
                Email = "student@uniprint.edu.vn",
                PasswordHash = defaultHash,
                Role = UserRole.Student,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        modelBuilder.Entity<PriceConfig>().HasData(
            new PriceConfig
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                PricePerBWPage = 350m,
                PricePerColorPage = 1500m,
                DoubleSidedDiscountPercent = 10m,
                BindingPrice = 5000m,
                StaplePrice = 1000m,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        modelBuilder.Entity<Shelf>().HasData(
            new Shelf { Id = Guid.Parse("55555555-5555-5555-5555-555555555501"), ShelfCode = "A1", Location = "Tầng 1 - Kệ Trái", MaxCapacity = 20, CurrentCount = 0, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Shelf { Id = Guid.Parse("55555555-5555-5555-5555-555555555502"), ShelfCode = "A2", Location = "Tầng 1 - Kệ Trái", MaxCapacity = 20, CurrentCount = 0, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Shelf { Id = Guid.Parse("55555555-5555-5555-5555-555555555503"), ShelfCode = "B1", Location = "Tầng 1 - Kệ Phải", MaxCapacity = 20, CurrentCount = 0, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        modelBuilder.Entity<Category>().HasData(
            new Category { Id = Guid.Parse("66666666-6666-6666-6666-666666666601"), Name = "Slide Bài Giảng", Description = "Tài liệu trình chiếu từ giảng viên", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Category { Id = Guid.Parse("66666666-6666-6666-6666-666666666602"), Name = "Đề Thi & Đáp Án", Description = "Tổng hợp đề thi các kỳ trước", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Category { Id = Guid.Parse("66666666-6666-6666-6666-666666666603"), Name = "Tóm Tắt Ôn Tập", Description = "Cheat sheet & tóm tắt kiến thức cốt lõi", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        modelBuilder.Entity<Subject>().HasData(
            new Subject { Id = Guid.Parse("77777777-7777-7777-7777-777777777701"), Code = "PRN231", Name = "Building Cross-Platform Applications with .NET", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Subject { Id = Guid.Parse("77777777-7777-7777-7777-777777777702"), Code = "SWD392", Name = "Software Architecture and Design", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
