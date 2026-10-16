using FVN_REGISTER.Application.Interfaces.Auths;
using FVN_REGISTER.Application.Interfaces.PublicInformation;
using FVN_REGISTER.Contract.Dtos.Approvals;
using FVN_REGISTER.Contract.Dtos.PublicInformation;
using FVN_REGISTER.Core.Entities.Common;
using Microsoft.Extensions.Hosting;
using FVN_REGISTER.Contract.Requests.PublicInformation;
using FVN_REGISTER.Contract.Utils;
using FVN_REGISTER.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FVN_REGISTER.Infrastructure.Services.PublicInformation;

public sealed class PublicInformationService : IPublicInformationService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly IHostEnvironment _env;
    public PublicInformationService(IUnitOfWork uow, IAuditService audit, IHostEnvironment env) { _uow = uow; _audit = audit; _env = env; }

    // Same limits/extensions the Execution evidence upload already uses.
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx" };
    private const int MaxAttachmentsPerItem = 20;

    public async Task<List<PublicInformationDto>> GetPublishedAsync(CancellationToken ct = default)
    {
        var rows = await _uow.Repository<F03PublicInformation>().Query().AsNoTracking()
            .Where(x => x.Status == "Published"
                && (!x.EffectiveFrom.HasValue || x.EffectiveFrom <= DateTime.Now)
                && (!x.EffectiveTo.HasValue || x.EffectiveTo >= DateTime.Now))
            .OrderByDescending(x => x.IsImportant)
            .ThenByDescending(x => x.PublishedAt)
            .ToListAsync(ct);
        return await EnrichAsync(rows.Select(MapEntity).ToList(), ct);
    }

    public async Task<(int Count, bool HasImportant, DateTime? LatestPublishedAt)> GetPublishedSummaryAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var query = _uow.Repository<F03PublicInformation>().Query().AsNoTracking()
            .Where(x => x.Status == "Published"
                && (!x.EffectiveFrom.HasValue || x.EffectiveFrom <= now)
                && (!x.EffectiveTo.HasValue || x.EffectiveTo >= now));

        var count = await query.CountAsync(ct);
        if (count == 0) return (0, false, null);

        var hasImportant = await query.AnyAsync(x => x.IsImportant, ct);
        var latest = await query.MaxAsync(x => x.PublishedAt, ct);
        return (count, hasImportant, latest);
    }

    public async Task<List<PublicInformationDto>> GetManageListAsync(CancellationToken ct = default)
    {
        var rows = await _uow.Repository<F03PublicInformation>().Query().AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return await EnrichAsync(rows.Select(MapEntity).ToList(), ct);
    }

    public async Task<PublicInformationDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<F03PublicInformation>().Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return null;
        return (await EnrichAsync(new List<PublicInformationDto> { MapEntity(entity) }, ct))[0];
    }

    public async Task<ServiceResult<PublicInformationDto>> CreateAsync(SavePublicInformationRequest request, int actorUserId, CancellationToken ct = default)
    {
        Validate(request);
        var entity = new F03PublicInformation { Type=request.Type.Trim(), Title=request.Title.Trim(), Summary=request.Summary?.Trim(), Content=request.Content, Status="Draft", EffectiveFrom=request.EffectiveFrom, EffectiveTo=request.EffectiveTo, IsImportant=request.IsImportant, AttachmentUrl=request.AttachmentUrl?.Trim(), CreatedBy=actorUserId };
        await _uow.Repository<F03PublicInformation>().AddAsync(entity, ct); await _uow.SaveChangesAsync(ct);
        await _audit.LogAction("PUBLIC_INFO_CREATED", actorUserId, $"Id={entity.Id}; Title={entity.Title}", ct:ct);
        return ServiceResult<PublicInformationDto>.Ok(MapEntity(entity));
    }

    public async Task<ServiceResult<PublicInformationDto>> UpdateAsync(int id, SavePublicInformationRequest request, int actorUserId, CancellationToken ct = default)
    {
        Validate(request);
        var entity = await _uow.Repository<F03PublicInformation>().Query().FirstOrDefaultAsync(x=>x.Id==id,ct);
        if(entity==null) return ServiceResult<PublicInformationDto>.Fail("Không tìm thấy nội dung công khai.");
        if(entity.Status=="Published") return ServiceResult<PublicInformationDto>.Fail("Không sửa trực tiếp nội dung đã Publish; hãy Archive và tạo phiên bản mới.");
        entity.Type=request.Type.Trim(); entity.Title=request.Title.Trim(); entity.Summary=request.Summary?.Trim(); entity.Content=request.Content; entity.EffectiveFrom=request.EffectiveFrom; entity.EffectiveTo=request.EffectiveTo; entity.IsImportant=request.IsImportant; entity.AttachmentUrl=request.AttachmentUrl?.Trim(); entity.ModifiedBy=actorUserId; entity.ModifiedAt=DateTime.Now;
        await _uow.SaveChangesAsync(ct); await _audit.LogAction("PUBLIC_INFO_UPDATED",actorUserId,$"Id={id}; Title={entity.Title}",ct:ct); return ServiceResult<PublicInformationDto>.Ok(MapEntity(entity));
    }

    public async Task<ServiceResult> PublishAsync(int id,int actorUserId,CancellationToken ct=default)
    { var e=await _uow.Repository<F03PublicInformation>().Query().FirstOrDefaultAsync(x=>x.Id==id,ct); if(e==null)return ServiceResult.Fail("Không tìm thấy nội dung."); if(e.Status=="Archived")return ServiceResult.Fail("Nội dung đã Archive."); e.Status="Published";e.PublishedBy=actorUserId;e.PublishedAt=DateTime.Now;e.ModifiedBy=actorUserId;e.ModifiedAt=DateTime.Now;await _uow.SaveChangesAsync(ct);await _audit.LogAction("PUBLIC_INFO_PUBLISHED",actorUserId,$"Id={id}; Title={e.Title}",ct:ct);return ServiceResult.Ok(); }
    public async Task<ServiceResult> ArchiveAsync(int id,int actorUserId,CancellationToken ct=default)
    { var e=await _uow.Repository<F03PublicInformation>().Query().FirstOrDefaultAsync(x=>x.Id==id,ct); if(e==null)return ServiceResult.Fail("Không tìm thấy nội dung."); e.Status="Archived";e.ModifiedBy=actorUserId;e.ModifiedAt=DateTime.Now;await _uow.SaveChangesAsync(ct);await _audit.LogAction("PUBLIC_INFO_ARCHIVED",actorUserId,$"Id={id}; Title={e.Title}",ct:ct);return ServiceResult.Ok(); }

    // ---- attachments (signed / stamped documents) --------------------------------------------------------------

    /// <summary>One query for N items (no N+1).</summary>
    private async Task<List<PublicInformationDto>> EnrichAsync(List<PublicInformationDto> items, CancellationToken ct)
    {
        if (items.Count == 0) return items;
        var ids = items.Select(x => x.Id).ToList();
        var files = await _uow.Repository<F03PublicInformationAttachment>().Query().AsNoTracking()
            .Where(x => ids.Contains(x.PublicInformationId)).OrderBy(x => x.Id).ToListAsync(ct);
        var byInfo = files.GroupBy(x => x.PublicInformationId).ToDictionary(g => g.Key, g => g.Select(ToAttachmentDto).ToList());
        foreach (var item in items) if (byInfo.TryGetValue(item.Id, out var list)) item.Attachments = list;
        return items;
    }

    private static AttachmentDto ToAttachmentDto(F03PublicInformationAttachment x) => new()
    {
        FileId = x.Id, FileName = x.FileName, FileExtension = x.FileExtension, FileSize = x.FileSize, UploadedAt = x.CreatedAt
    };

    public async Task<ServiceResult<AttachmentDto>> AddAttachmentAsync(int id, string fileName, long length, Stream content, int actorUserId, CancellationToken ct = default)
    {
        var info = await _uow.Repository<F03PublicInformation>().Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (info == null) return ServiceResult<AttachmentDto>.Fail("Không tìm thấy nội dung.");
        if (info.Status != "Draft") return ServiceResult<AttachmentDto>.Fail("Chỉ đính kèm file khi nội dung còn ở trạng thái Draft.");
        if (length <= 0 || length > MaxAttachmentBytes) return ServiceResult<AttachmentDto>.Fail("File phải lớn hơn 0 và không vượt quá 10 MB.");

        var safeName = Path.GetFileName(fileName ?? string.Empty);
        var extension = Path.GetExtension(safeName)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(safeName) || string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            return ServiceResult<AttachmentDto>.Fail("Định dạng file không được hỗ trợ (PDF, JPG, PNG, DOC(X), XLS(X)).");

        var count = await _uow.Repository<F03PublicInformationAttachment>().Query().CountAsync(x => x.PublicInformationId == id, ct);
        if (count >= MaxAttachmentsPerItem) return ServiceResult<AttachmentDto>.Fail($"Tối đa {MaxAttachmentsPerItem} file cho mỗi nội dung.");

        var folder = Path.Combine(_env.ContentRootPath, "uploads", "public-information", DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM"));
        Directory.CreateDirectory(folder);
        var storedPath = Path.Combine(folder, $"{Guid.NewGuid():N}{extension}");

        await using (var output = new FileStream(storedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await content.CopyToAsync(output, ct);

        try
        {
            var entity = new F03PublicInformationAttachment
            {
                PublicInformationId = id, FileName = safeName, FileExtension = extension, FileSize = length,
                FilePath = Path.GetRelativePath(_env.ContentRootPath, storedPath), CreatedBy = actorUserId, CreatedAt = DateTime.Now
            };
            await _uow.Repository<F03PublicInformationAttachment>().AddAsync(entity, ct);
            await _uow.SaveChangesAsync(ct);
            await _audit.LogAction("PUBLIC_INFO_ATTACHMENT_ADDED", actorUserId, $"Id={id}; File={safeName}", ct: ct);
            return ServiceResult<AttachmentDto>.Ok(ToAttachmentDto(entity));
        }
        catch
        {
            try { File.Delete(storedPath); } catch { /* keep the original DB error */ }
            throw;
        }
    }

    public async Task<ServiceResult> RemoveAttachmentAsync(int id, int attachmentId, int actorUserId, CancellationToken ct = default)
    {
        var info = await _uow.Repository<F03PublicInformation>().Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (info == null) return ServiceResult.Fail("Không tìm thấy nội dung.");
        if (info.Status != "Draft") return ServiceResult.Fail("Chỉ xoá file khi nội dung còn ở trạng thái Draft.");
        var file = await _uow.Repository<F03PublicInformationAttachment>().Query().FirstOrDefaultAsync(x => x.Id == attachmentId && x.PublicInformationId == id, ct);
        if (file == null) return ServiceResult.Fail("Không tìm thấy file đính kèm.");

        _uow.Repository<F03PublicInformationAttachment>().Remove(file);
        await _uow.SaveChangesAsync(ct);
        try { File.Delete(Path.Combine(_env.ContentRootPath, file.FilePath)); } catch { /* orphan file is harmless */ }
        await _audit.LogAction("PUBLIC_INFO_ATTACHMENT_REMOVED", actorUserId, $"Id={id}; File={file.FileName}", ct: ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<PublicInformationFileDto>> OpenAttachmentAsync(int id, int attachmentId, bool publishedOnly, CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var info = await _uow.Repository<F03PublicInformation>().Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (info == null) return ServiceResult<PublicInformationFileDto>.Fail("Không tìm thấy nội dung.");
        if (publishedOnly && !(info.Status == "Published"
                && (!info.EffectiveFrom.HasValue || info.EffectiveFrom <= now)
                && (!info.EffectiveTo.HasValue || info.EffectiveTo >= now)))
            return ServiceResult<PublicInformationFileDto>.Fail("Không tìm thấy file đính kèm.");

        var file = await _uow.Repository<F03PublicInformationAttachment>().Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == attachmentId && x.PublicInformationId == id, ct);
        if (file == null) return ServiceResult<PublicInformationFileDto>.Fail("Không tìm thấy file đính kèm.");

        // FilePath is relative to ContentRoot; refuse anything that resolves outside the uploads folder.
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "uploads", "public-information")) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(_env.ContentRootPath, file.FilePath));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
            return ServiceResult<PublicInformationFileDto>.Fail("File không còn tồn tại trên máy chủ.");

        return ServiceResult<PublicInformationFileDto>.Ok(new PublicInformationFileDto
        {
            Content = await File.ReadAllBytesAsync(full, ct), FileName = file.FileName, ContentType = ContentTypeOf(file.FileExtension)
        });
    }

    private static string ContentTypeOf(string? ext) => (ext ?? string.Empty).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf", ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png",
        ".doc" => "application/msword", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel", ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream"
    };

    private static void Validate(SavePublicInformationRequest x){if(string.IsNullOrWhiteSpace(x.Title))throw new ArgumentException("Tiêu đề là bắt buộc.");if(x.EffectiveFrom.HasValue&&x.EffectiveTo.HasValue&&x.EffectiveTo<x.EffectiveFrom)throw new ArgumentException("Thời điểm kết thúc phải sau thời điểm bắt đầu.");if(!new[]{"Announcement","News","HRContent","CompanyInfo","PublicContent"}.Contains(x.Type,StringComparer.OrdinalIgnoreCase))throw new ArgumentException("Loại thông tin không hợp lệ.");}
    private static PublicInformationDto Map(F03PublicInformation x)=>MapEntity(x);
    private static PublicInformationDto MapEntity(F03PublicInformation x)=>new(){Id=x.Id,Type=x.Type,Title=x.Title,Summary=x.Summary,Content=x.Content,Status=x.Status,EffectiveFrom=x.EffectiveFrom,EffectiveTo=x.EffectiveTo,IsImportant=x.IsImportant,AttachmentUrl=x.AttachmentUrl,PublishedAt=x.PublishedAt};
}
