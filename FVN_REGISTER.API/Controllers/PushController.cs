using FVN_REGISTER.Application.Configuration;
using FVN_REGISTER.Application.Interfaces.Notifications;
using FVN_REGISTER.Contract.Dtos.Notifications;
using FVN_REGISTER.Application.Interfaces.Users;
using FVN_REGISTER.Contract.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FVN_REGISTER.API.Controllers
{
    /// <summary>Registers the current device for Web Push (app-icon unread badge).</summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PushController : BaseApiController
    {
        private readonly IWebPushService _webPush;

        public PushController(
            IWebPushService webPush,
            ICurrentUserService currentUser,
            IUserLogService userLog,
            ILogger<PushController> logger,
            IOptionsMonitor<AuthDebugOptions> options)
            : base(currentUser, userLog, logger, options)
        {
            _webPush = webPush;
        }

        [HttpGet("public-key")]
        public IActionResult GetPublicKey()
        {
            if (UserInfo == null) return Unauthorized();

            var key = _webPush.PublicKey;
            return string.IsNullOrEmpty(key)
                ? NotFound(ApiResponse<object>.Fail("Web Push chưa được cấu hình."))
                : Ok(ApiResponse<object>.Ok(new { publicKey = key }));
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] PushSubscribeRequestDto dto, CancellationToken ct = default)
        {
            if (UserInfo == null) return Unauthorized();

            var result = await _webPush.SubscribeAsync(UserInfo.UserId, dto, ct);
            return result.IsSuccess
                ? Ok(ApiResponse<object>.Ok(new { }))
                : BadRequest(ApiResponse<object>.Fail(result.Message ?? "Không thể đăng ký thiết bị."));
        }

        [HttpPost("unsubscribe")]
        public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeRequestDto dto, CancellationToken ct = default)
        {
            if (UserInfo == null) return Unauthorized();

            await _webPush.UnsubscribeAsync(UserInfo.UserId, dto?.Endpoint ?? string.Empty, ct);
            return Ok(ApiResponse<object>.Ok(new { }));
        }
    }
}
