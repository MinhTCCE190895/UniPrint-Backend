
using Microsoft.EntityFrameworkCore;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Business.Services;

public interface IStudyHubService
{
    // Lấy danh sách tài liệu đã duyệt
    Task<ApiResponse<List<StudyMaterialDto>>> GetApprovedMaterialsAsync(
        string? search,
        Guid? subjectId,
        Guid? categoryId);

    // Lấy tài liệu của sinh viên
    Task<ApiResponse<List<StudyMaterialDto>>> GetMyMaterialsAsync(
        Guid studentId);

    // Đăng tải tài liệu
    Task<ApiResponse<StudyMaterialDto>> UploadMaterialAsync(
        Guid studentId,
        CreateMaterialDto request);

    // Admin lấy tài liệu chờ duyệt
    Task<ApiResponse<List<StudyMaterialDto>>> GetPendingMaterialsForAdminAsync();

    // Admin duyệt hoặc từ chối
    Task<ApiResponse<bool>> ModerateMaterialAsync(
        Guid materialId,
        MaterialStatus newStatus);

    // BR05: Tạo hoặc cập nhật đánh giá
    Task<ApiResponse<bool>> AddReviewAsync(
        Guid materialId,
        Guid studentId,
        MaterialReviewDto reviewDto,
        CancellationToken ct = default);

    // Báo cáo tài liệu
    Task<ApiResponse<bool>> ReportMaterialAsync(
        Guid materialId,
        Guid studentId,
        MaterialReportDto reportDto);

    // Tăng lượt tải
    Task<ApiResponse<bool>> IncrementDownloadCountAsync(
        Guid materialId,
        CancellationToken ct = default);
}

public class StudyHubService : IStudyHubService
{
    private readonly IUnitOfWork _unitOfWork;

    public StudyHubService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    // ==========================================
    // 1. GET APPROVED MATERIALS
    // ==========================================

    public async Task<ApiResponse<List<StudyMaterialDto>>>
        GetApprovedMaterialsAsync(
            string? search,
            Guid? subjectId,
            Guid? categoryId)
    {
        var query = _unitOfWork.Repository<StudyMaterial>()
            .Query()
            .AsNoTracking()
            .Where(m => m.Status == MaterialStatus.Approved);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();

            query = query.Where(m =>
                m.Title.Contains(keyword) ||
                m.Description.Contains(keyword) ||
                m.Subject.Code.Contains(keyword) ||
                m.Subject.Name.Contains(keyword));
        }

        if (subjectId.HasValue)
        {
            query = query.Where(
                m => m.SubjectId == subjectId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(
                m => m.CategoryId == categoryId.Value);
        }

        var materials = await query
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new StudyMaterialDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                DocumentId = m.DocumentId,
                DocumentFileName = m.Document.FileName,
                SubjectCode = m.Subject.Code,
                SubjectName = m.Subject.Name,
                CategoryName = m.Category.Name,
                UploaderName = m.UploadedByStudent.FullName,
                Status = m.Status,
                DownloadCount = m.DownloadCount,
                PrintCount = m.PrintCount,
                AverageRating = m.AverageRating,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<StudyMaterialDto>>.Ok(
            materials);
    }

    // ==========================================
    // 2. GET MY MATERIALS
    // ==========================================

    public async Task<ApiResponse<List<StudyMaterialDto>>>
        GetMyMaterialsAsync(Guid studentId)
    {
        var materials = await _unitOfWork
            .Repository<StudyMaterial>()
            .Query()
            .AsNoTracking()
            .Where(m => m.Document.UserId == studentId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new StudyMaterialDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                DocumentId = m.DocumentId,
                DocumentFileName = m.Document.FileName,
                SubjectCode = m.Subject.Code,
                SubjectName = m.Subject.Name,
                CategoryName = m.Category.Name,
                UploaderName = m.UploadedByStudent.FullName,
                Status = m.Status,
                DownloadCount = m.DownloadCount,
                PrintCount = m.PrintCount,
                AverageRating = m.AverageRating,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<StudyMaterialDto>>.Ok(
            materials);
    }

    // ==========================================
    // 3. CREATE STUDY MATERIAL - BR04
    // ==========================================

    public async Task<ApiResponse<StudyMaterialDto>>
        UploadMaterialAsync(
            Guid studentId,
            CreateMaterialDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return ApiResponse<StudyMaterialDto>.Fail(
                "Tên tài liệu không được để trống.");
        }

        var document = await _unitOfWork
            .Repository<Document>()
            .Query()
            .FirstOrDefaultAsync(d =>
                d.Id == request.DocumentId &&
                d.UserId == studentId);

        if (document == null)
        {
            return ApiResponse<StudyMaterialDto>.Fail(
                "Tài liệu không tồn tại hoặc không thuộc sinh viên.");
        }

        var subjectExists = await _unitOfWork
            .Repository<Subject>()
            .Query()
            .AnyAsync(s => s.Id == request.SubjectId);

        if (!subjectExists)
        {
            return ApiResponse<StudyMaterialDto>.Fail(
                "Môn học không tồn tại.");
        }

        var categoryExists = await _unitOfWork
            .Repository<Category>()
            .Query()
            .AnyAsync(c => c.Id == request.CategoryId);

        if (!categoryExists)
        {
            return ApiResponse<StudyMaterialDto>.Fail(
                "Danh mục không tồn tại.");
        }

        var material = new StudyMaterial
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim()
                ?? string.Empty,
            DocumentId = request.DocumentId,
            SubjectId = request.SubjectId,
            CategoryId = request.CategoryId,
            Status = MaterialStatus.Pending,
            DownloadCount = 0,
            PrintCount = 0,
            AverageRating = 0
        };

        await _unitOfWork
            .Repository<StudyMaterial>()
            .AddAsync(material);

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<StudyMaterialDto>.Ok(
            new StudyMaterialDto
            {
                Id = material.Id,
                Title = material.Title,
                Description = material.Description,
                DocumentId = material.DocumentId,
                Status = material.Status,
                CreatedAt = material.CreatedAt
            },
            "Đăng tải thành công! Tài liệu đang chờ Admin duyệt.");
    }

    // ==========================================
    // 4. GET PENDING MATERIALS - ADMIN
    // ==========================================

    public async Task<ApiResponse<List<StudyMaterialDto>>>
        GetPendingMaterialsForAdminAsync()
    {
        var materials = await _unitOfWork
            .Repository<StudyMaterial>()
            .Query()
            .AsNoTracking()
            .Where(m => m.Status == MaterialStatus.Pending)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new StudyMaterialDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                DocumentId = m.DocumentId,
                DocumentFileName = m.Document.FileName,
                SubjectCode = m.Subject.Code,
                SubjectName = m.Subject.Name,
                CategoryName = m.Category.Name,
                UploaderName = m.UploadedByStudent.FullName,
                Status = m.Status,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<StudyMaterialDto>>.Ok(
            materials);
    }

    // ==========================================
    // 5. MODERATE MATERIAL - ADMIN
    // ==========================================

    public async Task<ApiResponse<bool>> ModerateMaterialAsync(
        Guid materialId,
        MaterialStatus newStatus)
    {
        if (newStatus != MaterialStatus.Approved &&
            newStatus != MaterialStatus.Rejected)
        {
            return ApiResponse<bool>.Fail(
                "Trạng thái kiểm duyệt không hợp lệ.");
        }

        var material = await _unitOfWork
            .Repository<StudyMaterial>()
            .GetByIdAsync(materialId);

        if (material == null)
        {
            return ApiResponse<bool>.Fail(
                "Không tìm thấy tài liệu.");
        }

        if (material.Status != MaterialStatus.Pending)
        {
            return ApiResponse<bool>.Fail(
                "Chỉ có thể duyệt tài liệu đang chờ duyệt.");
        }

        material.Status = newStatus;

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<bool>.Ok(
            true,
            newStatus == MaterialStatus.Approved
                ? "Duyệt tài liệu thành công."
                : "Từ chối tài liệu thành công.");
    }

    // ==========================================
    // 6. CREATE / UPDATE REVIEW & RATING - BR05
    // ==========================================

    public async Task<ApiResponse<bool>> AddReviewAsync(
        Guid materialId,
        Guid studentId,
        MaterialReviewDto reviewDto,
        CancellationToken ct = default)
    {
        // Kiểm tra số sao
        if (reviewDto.Rating < 1 || reviewDto.Rating > 5)
        {
            return ApiResponse<bool>.Fail(
                "Đánh giá phải từ 1 đến 5 sao.");
        }

        // Kiểm tra độ dài nhận xét
        if (reviewDto.Comment?.Length > 1000)
        {
            return ApiResponse<bool>.Fail(
                "Nhận xét không được vượt quá 1000 ký tự.");
        }

        // Kiểm tra tài liệu tồn tại và đã duyệt
        var material = await _unitOfWork
            .Repository<StudyMaterial>()
            .Query()
            .FirstOrDefaultAsync(
                m => m.Id == materialId,
                ct);

        if (material == null)
        {
            return ApiResponse<bool>.Fail(
                "Không tìm thấy tài liệu.");
        }

        if (material.Status != MaterialStatus.Approved)
        {
            return ApiResponse<bool>.Fail(
                "Chỉ được đánh giá tài liệu đã kiểm duyệt.");
        }

        // Kiểm tra sinh viên đã đánh giá chưa
        var reviewRepo = _unitOfWork
            .Repository<MaterialReview>();

        var existingReview = await reviewRepo
            .Query()
            .FirstOrDefaultAsync(
                r => r.MaterialId == materialId &&
                     r.StudentId == studentId,
                ct);

        bool isUpdate = existingReview != null;

        if (existingReview == null)
        {
            // CREATE REVIEW
            var review = new MaterialReview
            {
                MaterialId = materialId,
                StudentId = studentId,
                Rating = reviewDto.Rating,
                Comment = reviewDto.Comment?.Trim()
            };

            await reviewRepo.AddAsync(review, ct);
        }
        else
        {
            // UPDATE REVIEW
            existingReview.Rating = reviewDto.Rating;
            existingReview.Comment =
                reviewDto.Comment?.Trim();
        }

        // Lưu thay đổi trước khi tính trung bình
        await _unitOfWork.SaveChangesAsync(ct);

        // Tính lại AverageRating từ tất cả review
        var averageRating = await reviewRepo
            .Query()
            .Where(r => r.MaterialId == materialId)
            .AverageAsync(
                r => (double?)r.Rating,
                ct) ?? 0;

        material.AverageRating = averageRating;

        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(
            true,
            isUpdate
                ? "Cập nhật đánh giá thành công!"
                : "Đánh giá tài liệu thành công!");
    }

    // ==========================================
    // 7. REPORT MATERIAL
    // ==========================================

    public async Task<ApiResponse<bool>> ReportMaterialAsync(
        Guid materialId,
        Guid studentId,
        MaterialReportDto reportDto)
    {
        if (string.IsNullOrWhiteSpace(reportDto.Reason))
        {
            return ApiResponse<bool>.Fail(
                "Vui lòng nhập lý do báo cáo.");
        }

        var materialExists = await _unitOfWork
            .Repository<StudyMaterial>()
            .Query()
            .AnyAsync(m =>
                m.Id == materialId &&
                m.Status == MaterialStatus.Approved);

        if (!materialExists)
        {
            return ApiResponse<bool>.Fail(
                "Tài liệu không tồn tại hoặc chưa được duyệt.");
        }

        // Phần lưu báo cáo cần sử dụng đúng entity
        // MaterialReport trong dự án hiện tại.
        // Không tự tạo cấu trúc bảng hoặc entity mới
        // để tránh ảnh hưởng code của thành viên khác.

        return ApiResponse<bool>.Fail(
            "Chức năng lưu báo cáo cần kết nối với entity báo cáo hiện tại.");
    }

    // ==========================================
    // 8. INCREMENT DOWNLOAD COUNT
    // ==========================================

    public async Task<ApiResponse<bool>>
        IncrementDownloadCountAsync(
            Guid materialId,
            CancellationToken ct = default)
    {
        var material = await _unitOfWork
            .Repository<StudyMaterial>()
            .Query()
            .FirstOrDefaultAsync(
                m => m.Id == materialId &&
                     m.Status == MaterialStatus.Approved,
                ct);

        if (material == null)
        {
            return ApiResponse<bool>.Fail(
                "Tài liệu không tồn tại hoặc chưa được duyệt.");
        }

        material.DownloadCount++;

        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(
            true,
            "Cập nhật lượt tải thành công.");
    }
}
