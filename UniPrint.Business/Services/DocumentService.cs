using Microsoft.EntityFrameworkCore;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Business.Services;

public interface IDocumentService
{
    /// <summary>
    /// Task 6: Tải tài liệu PDF/DOCX lên hệ thống, tự động đọc số trang và kích thước file
    /// </summary>
    Task<ApiResponse<DocumentResponseDto>> UploadDocumentStreamAsync(
        Guid userId,
        Stream fileStream,
        string originalFileName,
        long fileSizeBytes,
        string? targetStorageFolder = null,
        CancellationToken ct = default);

    /// <summary>
    /// Tải tài liệu bằng DTO (hỗ trợ tương thích ngược)
    /// </summary>
    Task<ApiResponse<DocumentResponseDto>> UploadDocumentAsync(
        Guid userId,
        DocumentUploadDto uploadDto,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách tài liệu của sinh viên hiện tại
    /// </summary>
    Task<ApiResponse<List<DocumentResponseDto>>> GetMyDocumentsAsync(
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy thông tin tài liệu theo ID
    /// </summary>
    Task<ApiResponse<DocumentResponseDto>> GetDocumentByIdAsync(
        Guid documentId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Task 7: Xóa tài liệu cá nhân đã tải lên (chỉ chủ sở hữu tài liệu mới được xóa)
    /// </summary>
    Task<ApiResponse<bool>> DeleteDocumentAsync(
        Guid documentId,
        Guid userId,
        CancellationToken ct = default);
}

public class DocumentService : IDocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDocumentInspector _documentInspector;

    public DocumentService(IUnitOfWork unitOfWork, IDocumentInspector? documentInspector = null)
    {
        _unitOfWork = unitOfWork;
        _documentInspector = documentInspector ?? new DocumentInspector();
    }

    public async Task<ApiResponse<DocumentResponseDto>> UploadDocumentStreamAsync(
        Guid userId,
        Stream fileStream,
        string originalFileName,
        long fileSizeBytes,
        string? targetStorageFolder = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return ApiResponse<DocumentResponseDto>.Fail("Tên file không hợp lệ.");
        }

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!_documentInspector.IsSupportedExtension(extension))
        {
            return ApiResponse<DocumentResponseDto>.Fail($"Định dạng file '{extension}' không được hỗ trợ. Hệ thống chỉ chấp nhận file .pdf và .docx.");
        }

        if (fileSizeBytes <= 0)
        {
            return ApiResponse<DocumentResponseDto>.Fail("File rỗng không có nội dung dữ liệu.");
        }

        const long maxSizeBytes = 50 * 1024 * 1024; // 50MB
        if (fileSizeBytes > maxSizeBytes)
        {
            return ApiResponse<DocumentResponseDto>.Fail("Dung lượng file vượt quá giới hạn tối đa cho phép (50MB).");
        }

        // Tự động phân tích đọc số trang từ nội dung file (Task 6 & EC01)
        int pageCount;
        try
        {
            pageCount = _documentInspector.InspectPageCount(fileStream, extension);
        }
        catch (Exception ex)
        {
            return ApiResponse<DocumentResponseDto>.Fail($"Không thể phân tích tài liệu (EC01): {ex.Message}");
        }

        // Lưu file vào thư mục lưu trữ vật lý
        var storageFolder = targetStorageFolder ?? Path.Combine(AppContext.BaseDirectory, "uploads", "documents");
        Directory.CreateDirectory(storageFolder);

        var safeUniqueFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(originalFileName)}";
        var fullSavePath = Path.Combine(storageFolder, safeUniqueFileName);

        if (fileStream.CanSeek)
        {
            fileStream.Seek(0, SeekOrigin.Begin);
        }

        using (var outputStream = new FileStream(fullSavePath, FileMode.Create, FileAccess.Write))
        {
            await fileStream.CopyToAsync(outputStream, ct);
        }

        var document = new Document
        {
            UserId = userId,
            FileName = Path.GetFileName(originalFileName),
            FilePath = fullSavePath,
            FileExtension = extension,
            FileSizeBytes = fileSizeBytes,
            PageCount = pageCount
        };

        await _unitOfWork.Repository<Document>().AddAsync(document, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<DocumentResponseDto>.Ok(new DocumentResponseDto
        {
            Id = document.Id,
            FileName = document.FileName,
            FileExtension = document.FileExtension,
            FileSizeBytes = document.FileSizeBytes,
            PageCount = document.PageCount,
            CreatedAt = document.CreatedAt
        }, $"Upload tài liệu thành công! (Tự động nhận diện: {pageCount} trang, {FormatFileSize(fileSizeBytes)})");
    }

    public async Task<ApiResponse<DocumentResponseDto>> UploadDocumentAsync(Guid userId, DocumentUploadDto uploadDto, CancellationToken ct = default)
    {
        var document = new Document
        {
            UserId = userId,
            FileName = uploadDto.FileName,
            FilePath = uploadDto.TempStoragePath,
            FileExtension = uploadDto.FileExtension,
            FileSizeBytes = uploadDto.FileSizeBytes,
            PageCount = uploadDto.PageCount > 0 ? uploadDto.PageCount : 1
        };

        await _unitOfWork.Repository<Document>().AddAsync(document, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<DocumentResponseDto>.Ok(new DocumentResponseDto
        {
            Id = document.Id,
            FileName = document.FileName,
            FileExtension = document.FileExtension,
            FileSizeBytes = document.FileSizeBytes,
            PageCount = document.PageCount,
            CreatedAt = document.CreatedAt
        }, "Upload tài liệu thành công!");
    }

    public async Task<ApiResponse<List<DocumentResponseDto>>> GetMyDocumentsAsync(Guid userId, CancellationToken ct = default)
    {
        var docs = await _unitOfWork.Repository<Document>().Query()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentResponseDto
            {
                Id = d.Id,
                FileName = d.FileName,
                FileExtension = d.FileExtension,
                FileSizeBytes = d.FileSizeBytes,
                PageCount = d.PageCount,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<DocumentResponseDto>>.Ok(docs);
    }

    public async Task<ApiResponse<DocumentResponseDto>> GetDocumentByIdAsync(Guid documentId, Guid userId, CancellationToken ct = default)
    {
        var doc = await _unitOfWork.Repository<Document>().GetByIdAsync(documentId, ct);
        if (doc == null || doc.UserId != userId)
        {
            return ApiResponse<DocumentResponseDto>.Fail("Không tìm thấy tài liệu.");
        }

        return ApiResponse<DocumentResponseDto>.Ok(new DocumentResponseDto
        {
            Id = doc.Id,
            FileName = doc.FileName,
            FileExtension = doc.FileExtension,
            FileSizeBytes = doc.FileSizeBytes,
            PageCount = doc.PageCount,
            CreatedAt = doc.CreatedAt
        });
    }

    public async Task<ApiResponse<bool>> DeleteDocumentAsync(Guid documentId, Guid userId, CancellationToken ct = default)
    {
        var docRepo = _unitOfWork.Repository<Document>();
        var doc = await docRepo.GetByIdAsync(documentId, ct);

        // Kiểm tra quyền sở hữu (Task 7): Chỉ chủ sở hữu mới được xóa
        if (doc == null || doc.UserId != userId)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy tài liệu hoặc bạn không có quyền xóa tài liệu này.");
        }

        // Kiểm tra ràng buộc: Tài liệu có đang liên kết với đơn in nào không
        var isReferencedByOrder = await _unitOfWork.Repository<PrintOrder>().Query()
            .AnyAsync(o => o.DocumentId == documentId, ct);

        if (isReferencedByOrder)
        {
            return ApiResponse<bool>.Fail("Không thể xóa tài liệu vì tài liệu này đang được liên kết với đơn in trên hệ thống.");
        }

        // Xóa file vật lý trên ổ cứng nếu còn tồn tại
        if (!string.IsNullOrWhiteSpace(doc.FilePath) && File.Exists(doc.FilePath))
        {
            try
            {
                File.Delete(doc.FilePath);
            }
            catch
            {
                // Bỏ qua lỗi xóa file đĩa nếu bị lock
            }
        }

        docRepo.Delete(doc);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Xóa tài liệu thành công.");
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }
        if (bytes >= 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }
        return $"{bytes} bytes";
    }
}
