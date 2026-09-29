using Microsoft.EntityFrameworkCore;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Business.Services;

public interface IStudyHubService
{
    Task<ApiResponse<StudyMaterialDto>> UploadMaterialAsync(Guid studentId, CreateMaterialDto request, CancellationToken ct = default);
    Task<ApiResponse<List<StudyMaterialDto>>> GetApprovedMaterialsAsync(string? searchTerm, Guid? subjectId, Guid? categoryId, CancellationToken ct = default);
    Task<ApiResponse<List<StudyMaterialDto>>> GetPendingMaterialsForAdminAsync(CancellationToken ct = default);
    Task<ApiResponse<bool>> ModerateMaterialAsync(Guid materialId, MaterialStatus newStatus, CancellationToken ct = default);
    Task<ApiResponse<bool>> AddReviewAsync(Guid materialId, Guid studentId, MaterialReviewDto reviewDto, CancellationToken ct = default);
    Task<ApiResponse<bool>> ReportMaterialAsync(Guid materialId, Guid studentId, MaterialReportDto reportDto, CancellationToken ct = default);
    Task<ApiResponse<bool>> IncrementDownloadCountAsync(Guid materialId, CancellationToken ct = default);
}

public class StudyHubService : IStudyHubService
{
    private readonly IUnitOfWork _unitOfWork;

    public StudyHubService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<StudyMaterialDto>> UploadMaterialAsync(Guid studentId, CreateMaterialDto request, CancellationToken ct = default)
    {
        var doc = await _unitOfWork.Repository<Document>().GetByIdAsync(request.DocumentId, ct);
        if (doc == null)
        {
            return ApiResponse<StudyMaterialDto>.Fail("Không tìm thấy tài liệu đính kèm.");
        }

        var material = new StudyMaterial
        {
            Title = request.Title,
            Description = request.Description,
            DocumentId = request.DocumentId,
            SubjectId = request.SubjectId,
            CategoryId = request.CategoryId,
            UploadedByStudentId = studentId,
            Status = MaterialStatus.Pending // BR04: Phải chờ Admin duyệt
        };

        await _unitOfWork.Repository<StudyMaterial>().AddAsync(material, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<StudyMaterialDto>.Ok(new StudyMaterialDto
        {
            Id = material.Id,
            Title = material.Title,
            Description = material.Description,
            Status = material.Status,
            CreatedAt = material.CreatedAt
        }, "Tài liệu học tập đã được gửi duyệt lên Admin (BR04)!");
    }

    public async Task<ApiResponse<List<StudyMaterialDto>>> GetApprovedMaterialsAsync(string? searchTerm, Guid? subjectId, Guid? categoryId, CancellationToken ct = default)
    {
        var query = _unitOfWork.Repository<StudyMaterial>().Query()
            .Include(m => m.Subject)
            .Include(m => m.Category)
            .Include(m => m.UploadedByStudent)
            .Include(m => m.Document)
            .Where(m => m.Status == MaterialStatus.Approved); // EC08: Chỉ hiện tài liệu đã Approved

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(m => m.Title.ToLower().Contains(term) || m.Description.ToLower().Contains(term));
        }

        if (subjectId.HasValue) query = query.Where(m => m.SubjectId == subjectId.Value);
        if (categoryId.HasValue) query = query.Where(m => m.CategoryId == categoryId.Value);

        var list = await query
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new StudyMaterialDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                SubjectCode = m.Subject.Code,
                SubjectName = m.Subject.Name,
                CategoryName = m.Category.Name,
                UploaderName = m.UploadedByStudent.FullName,
                DocumentId = m.DocumentId,
                DocumentFileName = m.Document.FileName,
                Status = m.Status,
                DownloadCount = m.DownloadCount,
                PrintCount = m.PrintCount,
                AverageRating = m.AverageRating,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<StudyMaterialDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<StudyMaterialDto>>> GetPendingMaterialsForAdminAsync(CancellationToken ct = default)
    {
        var list = await _unitOfWork.Repository<StudyMaterial>().Query()
            .Include(m => m.Subject)
            .Include(m => m.Category)
            .Include(m => m.UploadedByStudent)
            .Include(m => m.Document)
            .Where(m => m.Status == MaterialStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new StudyMaterialDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                SubjectCode = m.Subject.Code,
                SubjectName = m.Subject.Name,
                CategoryName = m.Category.Name,
                UploaderName = m.UploadedByStudent.FullName,
                DocumentId = m.DocumentId,
                DocumentFileName = m.Document.FileName,
                Status = m.Status,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<StudyMaterialDto>>.Ok(list);
    }

    public async Task<ApiResponse<bool>> ModerateMaterialAsync(Guid materialId, MaterialStatus newStatus, CancellationToken ct = default)
    {
        var material = await _unitOfWork.Repository<StudyMaterial>().GetByIdAsync(materialId, ct);
        if (material == null) return ApiResponse<bool>.Fail("Không tìm thấy tài liệu.");

        material.Status = newStatus;
        _unitOfWork.Repository<StudyMaterial>().Update(material);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, $"Tài liệu đã được cập nhật trạng thái thành: {newStatus}.");
    }

    public async Task<ApiResponse<bool>> AddReviewAsync(Guid materialId, Guid studentId, MaterialReviewDto reviewDto, CancellationToken ct = default)
    {
        var reviewRepo = _unitOfWork.Repository<MaterialReview>();

        // BR05: Mỗi Student chỉ có 1 review/material
        var existing = (await reviewRepo.FindAsync(r => r.MaterialId == materialId && r.StudentId == studentId, ct)).FirstOrDefault();
        if (existing != null)
        {
            existing.Rating = reviewDto.Rating;
            existing.Comment = reviewDto.Comment;
            reviewRepo.Update(existing);
        }
        else
        {
            var review = new MaterialReview
            {
                MaterialId = materialId,
                StudentId = studentId,
                Rating = reviewDto.Rating,
                Comment = reviewDto.Comment
            };
            await reviewRepo.AddAsync(review, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // Tính lại điểm trung bình
        var reviews = await reviewRepo.FindAsync(r => r.MaterialId == materialId, ct);
        var material = await _unitOfWork.Repository<StudyMaterial>().GetByIdAsync(materialId, ct);
        if (material != null && reviews.Any())
        {
            material.AverageRating = Math.Round(reviews.Average(r => r.Rating), 1);
            _unitOfWork.Repository<StudyMaterial>().Update(material);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return ApiResponse<bool>.Ok(true, "Đánh giá tài liệu thành công!");
    }

    public async Task<ApiResponse<bool>> ReportMaterialAsync(Guid materialId, Guid studentId, MaterialReportDto reportDto, CancellationToken ct = default)
    {
        var report = new MaterialReport
        {
            MaterialId = materialId,
            ReportedByStudentId = studentId,
            Reason = reportDto.Reason
        };

        await _unitOfWork.Repository<MaterialReport>().AddAsync(report, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Đã gửi báo cáo vi phạm tới Ban Quản Trị.");
    }

    public async Task<ApiResponse<bool>> IncrementDownloadCountAsync(Guid materialId, CancellationToken ct = default)
    {
        var material = await _unitOfWork.Repository<StudyMaterial>().GetByIdAsync(materialId, ct);
        if (material == null) return ApiResponse<bool>.Fail("Không tìm thấy tài liệu.");

        material.DownloadCount += 1;
        _unitOfWork.Repository<StudyMaterial>().Update(material);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true);
    }
}
