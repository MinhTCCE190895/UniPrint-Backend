using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Pdf;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Context;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;
using Xunit;

namespace UniPrint.Tests.Services;

public class DocumentServiceTests : IDisposable
{
    private readonly UniPrintDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DocumentInspector _inspector;
    private readonly DocumentService _service;
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _otherStudentId = Guid.NewGuid();
    private readonly string _testTempFolder;

    public DocumentServiceTests()
    {
        var options = new DbContextOptionsBuilder<UniPrintDbContext>()
            .UseInMemoryDatabase(databaseName: $"UniPrint_Document_Test_{Guid.NewGuid()}")
            .Options;

        _dbContext = new UniPrintDbContext(options);
        _unitOfWork = new UnitOfWork(_dbContext);
        _inspector = new DocumentInspector();
        _service = new DocumentService(_unitOfWork, _inspector);

        _testTempFolder = Path.Combine(Path.GetTempPath(), $"uniprint_test_docs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testTempFolder);

        // Seed Users
        var student1 = new User { Id = _studentId, FullName = "Tran Huynh Khoi", Email = "khoi@uniprint.edu.vn", PasswordHash = "p", Role = UserRole.Student };
        var student2 = new User { Id = _otherStudentId, FullName = "Other Student", Email = "other@uniprint.edu.vn", PasswordHash = "p", Role = UserRole.Student };
        _dbContext.Users.AddRange(student1, student2);
        _dbContext.SaveChanges();
    }

    #region Task 6: Upload Document & Auto Page Count Tests

    [Fact]
    public async Task UploadDocument_ValidPdf_ShouldAutoCountPagesAndCalculateSize_AndSaveToDatabase()
    {
        // Arrange: Tạo file PDF thật có 7 trang trong bộ nhớ
        const int expectedPages = 7;
        var pdfBytes = GeneratePdfBytes(expectedPages);
        using var stream = new MemoryStream(pdfBytes);

        // Act (Task 6: Tải lên và tự động đọc số trang, kích thước)
        var result = await _service.UploadDocumentStreamAsync(
            _studentId,
            stream,
            "TaiLieu_OnThi_ToanCaoCap.pdf",
            pdfBytes.Length,
            targetStorageFolder: _testTempFolder);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.FileName.Should().Be("TaiLieu_OnThi_ToanCaoCap.pdf");
        result.Data.FileExtension.Should().Be(".pdf");
        result.Data.PageCount.Should().Be(expectedPages);
        result.Data.FileSizeBytes.Should().Be(pdfBytes.Length);

        // Kiểm tra database lưu đúng
        var dbDoc = await _dbContext.Documents.FindAsync(result.Data.Id);
        dbDoc.Should().NotBeNull();
        dbDoc!.UserId.Should().Be(_studentId);
        dbDoc.PageCount.Should().Be(expectedPages);
        dbDoc.FileSizeBytes.Should().Be(pdfBytes.Length);
        File.Exists(dbDoc.FilePath).Should().BeTrue();
    }

    [Fact]
    public async Task UploadDocument_ValidDocx_ShouldAutoCountPagesAndCalculateSize_AndSaveToDatabase()
    {
        // Arrange: Tạo file DOCX chuẩn có 15 trang
        const int expectedPages = 15;
        var docxBytes = GenerateDocxBytes(expectedPages);
        using var stream = new MemoryStream(docxBytes);

        // Act
        var result = await _service.UploadDocumentStreamAsync(
            _studentId,
            stream,
            "BaoCao_TieuLuan_PRN231.docx",
            docxBytes.Length,
            targetStorageFolder: _testTempFolder);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.FileName.Should().Be("BaoCao_TieuLuan_PRN231.docx");
        result.Data.FileExtension.Should().Be(".docx");
        result.Data.PageCount.Should().Be(expectedPages);
        result.Data.FileSizeBytes.Should().Be(docxBytes.Length);

        var dbDoc = await _dbContext.Documents.FindAsync(result.Data.Id);
        dbDoc.Should().NotBeNull();
        dbDoc!.PageCount.Should().Be(expectedPages);
    }

    [Fact]
    public async Task UploadDocument_UnsupportedExtension_ShouldFail_DueToEC01()
    {
        // Arrange: File đuôi không được hỗ trợ (.exe hoặc .txt)
        var content = Encoding.UTF8.GetBytes("Tập tin mã độc hại");
        using var stream = new MemoryStream(content);

        // Act
        var result = await _service.UploadDocumentStreamAsync(
            _studentId,
            stream,
            "virus_sample.exe",
            content.Length,
            targetStorageFolder: _testTempFolder);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không được hỗ trợ");

        // Không được lưu vào DB
        _dbContext.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadDocument_EmptyFile_ShouldFail()
    {
        // Arrange: File rỗng 0 bytes
        using var emptyStream = new MemoryStream();

        // Act
        var result = await _service.UploadDocumentStreamAsync(
            _studentId,
            emptyStream,
            "empty.pdf",
            0,
            targetStorageFolder: _testTempFolder);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("rỗng");
    }

    [Fact]
    public async Task UploadDocument_CorruptedPdf_ShouldFail_DueToEC01()
    {
        // Arrange: Dữ liệu hỏng không phải cấu trúc PDF hợp lệ
        var corruptedBytes = Encoding.UTF8.GetBytes("Corrupted file content - NOT A REAL PDF");
        using var stream = new MemoryStream(corruptedBytes);

        // Act (EC01: File bị hỏng hoặc định dạng lạ -> Hệ thống bắt lỗi, từ chối tạo Document)
        var result = await _service.UploadDocumentStreamAsync(
            _studentId,
            stream,
            "corrupted_document.pdf",
            corruptedBytes.Length,
            targetStorageFolder: _testTempFolder);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("EC01");
        _dbContext.Documents.Should().BeEmpty();
    }

    #endregion

    #region Task 7: Delete Document & Ownership Verification Tests

    [Fact]
    public async Task DeleteDocument_AsOwner_WithoutActiveOrders_ShouldSucceed_AndRemovePhysicalFile()
    {
        // Arrange: Tạo tài liệu của sinh viên
        var tempFilePath = Path.Combine(_testTempFolder, "file_to_delete.pdf");
        await File.WriteAllTextAsync(tempFilePath, "sample content");

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _studentId,
            FileName = "file_to_delete.pdf",
            FilePath = tempFilePath,
            FileExtension = ".pdf",
            FileSizeBytes = 1024,
            PageCount = 3
        };
        await _dbContext.Documents.AddAsync(doc);
        await _dbContext.SaveChangesAsync();

        // Act (Task 7: Xóa tài liệu cá nhân đã tải lên)
        var result = await _service.DeleteDocumentAsync(doc.Id, _studentId);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("thành công");

        // Kiểm tra xóa khỏi DB
        var deletedDoc = await _dbContext.Documents.FindAsync(doc.Id);
        deletedDoc.Should().BeNull();

        // Kiểm tra file vật lý đã được xóa dọn dẹp
        File.Exists(tempFilePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDocument_AsNonOwner_ShouldFail_WithPermissionError()
    {
        // Arrange: Tài liệu thuộc về _studentId
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _studentId,
            FileName = "private_exam.pdf",
            FilePath = "/uploads/private_exam.pdf",
            FileExtension = ".pdf",
            FileSizeBytes = 2048,
            PageCount = 5
        };
        await _dbContext.Documents.AddAsync(doc);
        await _dbContext.SaveChangesAsync();

        // Act: Sinh viên khác (_otherStudentId) cố ý xóa
        var result = await _service.DeleteDocumentAsync(doc.Id, _otherStudentId);

        // Assert: Chặn bảo vệ quyền sở hữu nghiêm ngặt
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("quyền xóa");

        // Tài liệu vẫn phải còn nguyên vẹn trong DB
        var retainedDoc = await _dbContext.Documents.FindAsync(doc.Id);
        retainedDoc.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteDocument_ReferencedByPrintOrder_ShouldFail_ToPreserveOrderIntegrity()
    {
        // Arrange: Tài liệu đang được sử dụng bởi một đơn in
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _studentId,
            FileName = "locked_in_order.pdf",
            FilePath = "/uploads/locked.pdf",
            FileExtension = ".pdf",
            FileSizeBytes = 4096,
            PageCount = 10
        };
        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-TEST-DELETE-LOCK",
            StudentId = _studentId,
            DocumentId = doc.Id,
            Status = PrintOrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.Documents.AddAsync(doc);
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.DeleteDocumentAsync(doc.Id, _studentId);

        // Assert: Chặn không cho xóa khi đang có đơn in liên kết
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("đơn in");

        var dbDoc = await _dbContext.Documents.FindAsync(doc.Id);
        dbDoc.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteDocument_NonExistent_ShouldFail()
    {
        // Act: Xóa Id không tồn tại
        var result = await _service.DeleteDocumentAsync(Guid.NewGuid(), _studentId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không tìm thấy");
    }

    [Fact]
    public async Task GetMyDocuments_ShouldReturnOnlyUserOwnedDocuments()
    {
        // Arrange: Thêm tài liệu cho cả 2 sinh viên
        var doc1 = new Document { Id = Guid.NewGuid(), UserId = _studentId, FileName = "doc1.pdf", FilePath = "p1", FileExtension = ".pdf", PageCount = 2, FileSizeBytes = 100, CreatedAt = DateTime.UtcNow.AddMinutes(-5) };
        var doc2 = new Document { Id = Guid.NewGuid(), UserId = _studentId, FileName = "doc2.docx", FilePath = "p2", FileExtension = ".docx", PageCount = 4, FileSizeBytes = 200, CreatedAt = DateTime.UtcNow };
        var docOther = new Document { Id = Guid.NewGuid(), UserId = _otherStudentId, FileName = "other.pdf", FilePath = "p3", FileExtension = ".pdf", PageCount = 8, FileSizeBytes = 300, CreatedAt = DateTime.UtcNow };

        await _dbContext.Documents.AddRangeAsync(doc1, doc2, docOther);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetMyDocumentsAsync(_studentId);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data!.Select(d => d.Id).Should().Contain(new[] { doc1.Id, doc2.Id });
        result.Data.Select(d => d.Id).Should().NotContain(docOther.Id);
    }

    #endregion

    #region Helper Methods for Generating Test Documents

    private static byte[] GeneratePdfBytes(int pageCount)
    {
        using var document = new PdfDocument();
        for (int i = 0; i < pageCount; i++)
        {
            document.AddPage();
        }
        using var ms = new MemoryStream();
        document.Save(ms);
        return ms.ToArray();
    }

    private static byte[] GenerateDocxBytes(int pageCount)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            // Thêm docProps/app.xml chứa <Pages>
            var appEntry = archive.CreateEntry("docProps/app.xml");
            using (var writer = new StreamWriter(appEntry.Open()))
            {
                writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Properties xmlns=""http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"">
    <Pages>{pageCount}</Pages>
</Properties>");
            }

            // Thêm word/document.xml bắt buộc của DOCX
            var docEntry = archive.CreateEntry("word/document.xml");
            using (var writer = new StreamWriter(docEntry.Open()))
            {
                writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?><w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main""><w:body><w:p><w:r><w:t>Demo content</w:t></w:r></w:p></w:body></w:document>");
            }
        }
        return ms.ToArray();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _unitOfWork.Dispose();
        if (Directory.Exists(_testTempFolder))
        {
            try
            {
                Directory.Delete(_testTempFolder, true);
            }
            catch
            {
                // Bỏ qua dọn dẹp thư mục tạm
            }
        }
    }

    #endregion
}
