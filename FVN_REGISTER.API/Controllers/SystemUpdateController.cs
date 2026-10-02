using FVN_REGISTER.Application.Interfaces.SystemUpdate;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Dtos.SystemUpdate;
using FVN_REGISTER.Contract.Responses;
using FVN_REGISTER.Core.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FVN_REGISTER.Application.Configuration;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.API.Controllers
{
    /// <summary>Accepts signed release packages. Deployment itself is performed by the privileged updater task.</summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public sealed class SystemUpdateController : BaseApiController
    {
        private readonly ISystemUpdateService _update;

        public SystemUpdateController(
            ISystemUpdateService update,
            ICurrentUserService currentUser,
            IUserLogService userLog,
            ILogger<SystemUpdateController> logger,
            IOptionsMonitor<AuthDebugOptions> options)
            : base(currentUser, userLog, logger, options) => _update = update;

        private bool IsSuperAdmin => UserInfo != null && UserInfo.Permission == UserPermissionCodes.SuperAdmin;

        [HttpGet("overview")]
        public async Task<IActionResult> Overview(CancellationToken ct = default)
        {
            if (UserInfo == null) return Unauthorized();
            if (!IsSuperAdmin) return Forbid();
            var result = await _update.GetOverviewAsync(ct);
            return result.IsSuccess
                ? Ok(ApiResponse<SystemUpdateOverviewDto>.Ok(result.Data!))
                : BadRequest(ApiResponse<SystemUpdateOverviewDto>.Fail(result.Message ?? "Không thể đọc trạng thái cập nhật."));
        }

        [HttpPost("upload")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct = default)
        {
            if (UserInfo == null) return Unauthorized();
            if (!IsSuperAdmin) return Forbid();
            if (file == null || file.Length == 0 || !file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse<PatchUploadResultDto>.Fail("Vui lòng chọn file gói vá (.zip)."));

            await using var stream = file.OpenReadStream();
            var result = await _update.SaveUploadAsync(stream, file.FileName, UserInfo.UserId, UserInfo.UserName ?? string.Empty, ct);
            return result.IsSuccess
                ? Ok(ApiResponse<PatchUploadResultDto>.Ok(result.Data!, "Đã nhận gói vá. Updater sẽ triển khai trong vòng 1 phút."))
                : BadRequest(ApiResponse<PatchUploadResultDto>.Fail(result.Message ?? "Không thể nhận gói vá."));
        }
    }
}