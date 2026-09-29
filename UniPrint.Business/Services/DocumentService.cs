using Microsoft.EntityFrameworkCore;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Business.Services;

public interface IDocumentService
{
    Task<ApiResponse<DocumentResponseDto>> UploadDocumentAsync(Guid userId, DocumentUploadDto uploadDto, CancellationToken ct = default);
    Task<ApiResponse<List<DocumentResponseDto>>> GetMyDocumentsAsync(Guid userId, CancellationToken ct = default);
    Task<ApiResponse<DocumentResponseDto>> GetDocumentByIdAsync(Guid documentId, Guid userId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteDocumentAsync(Guid documentId, Guid userId, CancellationToken ct = default);
}

public class DocumentService : IDocumentService
{
    private readonly IUnitOfWork _unitOfWork;

    public DocumentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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

        if (doc == null || doc.UserId != userId)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy tài liệu hoặc bạn không có quyền xóa.");
        }

        docRepo.Delete(doc);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Xóa tài liệu thành công.");
    }
}
