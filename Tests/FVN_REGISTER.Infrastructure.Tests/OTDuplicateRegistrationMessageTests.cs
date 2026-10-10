using FVN_REGISTER.Core.Enums;
using FVN_REGISTER.Infrastructure.Services.OT;
using Xunit;

namespace FVN_REGISTER.Infrastructure.Tests;

public sealed class OTDuplicateRegistrationMessageTests
{
    private static readonly DateTime Day = new(2026, 9, 24);

    [Fact]
    public void Describe_SelfRequest_NamesEmployeeRequestStatusAndSelf()
    {
        var text = OTDuplicateRegistrationFinder.Describe(
            new OTDuplicateRegistration("E001", "Nguyen Van A", 55, "OT-0055", ApprovalStatus.Pending, "E001", "Nguyen Van A"),
            Day);

        Assert.Contains("E001 - Nguyen Van A", text);
        Assert.Contains("24/09/2026", text);
        Assert.Contains("OT-0055", text);
        Assert.Contains("Chờ duyệt", text);
        Assert.Contains("do chính nhân viên tạo", text);
        Assert.Contains("một lần trong ngày", text);
    }

    [Fact]
    public void Describe_RequestedByColleague_NamesTheOtherRequester()
    {
        var text = OTDuplicateRegistrationFinder.Describe(
            new OTDuplicateRegistration("E001", "Nguyen Van A", 55, null, ApprovalStatus.Approved, "L010", "Tran Van Leader"),
            Day);

        Assert.Contains("#55", text);
        Assert.Contains("Đã duyệt", text);
        Assert.Contains("do L010 - Tran Van Leader đăng ký", text);
        Assert.DoesNotContain("do chính nhân viên tạo", text);
    }
}
