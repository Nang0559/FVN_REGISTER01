namespace FVN_REGISTER.Contract.Dtos.Help;

/// <summary>Question for the guide assistant (<c>POST api/ai/guide/ask</c>).</summary>
public sealed class GuideAskRequestDto
{
    public string Query { get; set; } = string.Empty;

    /// <summary>Feature code of the screen the user is reading, e.g. <c>OT</c>. Only used to rank passages; it never grants access.</summary>
    public string? Feature { get; set; }

    /// <summary>Last turns of this chat, oldest first. Untrusted: used only to resolve pronouns such as "nó" or "bước đó".</summary>
    public List<GuideChatTurnDto> History { get; set; } = new();
}

public sealed class GuideChatTurnDto
{
    /// <summary>"user" or "assistant".</summary>
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public sealed class GuideAnswerDto
{
    public string Answer { get; set; } = string.Empty;

    /// <summary>Supported | PartiallySupported | NotFound | Unavailable.</summary>
    public string Status { get; set; } = "NotFound";

    public List<GuideSourceDto> Sources { get; set; } = new();
    public string? Model { get; set; }
    public long ElapsedMilliseconds { get; set; }
}

public sealed class GuideSourceDto
{
    /// <summary>Breadcrumb of the guide passage, e.g. "2.2. Đăng ký làm thêm giờ (OT) › Kiểm tra hạn mức".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Feature code to open at <c>/huong-dan?feature=…</c>; empty for general passages that have no page of their own.</summary>
    public string? Feature { get; set; }
}
