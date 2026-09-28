using FVN_REGISTER.Core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FVN_REGISTER.Infrastructure.Models.Data.Configurations.Security;

public sealed class F03EndpointGovernancePolicyConfiguration : IEntityTypeConfiguration<F03EndpointGovernancePolicy>
{
    public void Configure(EntityTypeBuilder<F03EndpointGovernancePolicy> b)
    {
        b.ToTable("F03EndpointGovernancePolicies");
        b.HasKey(x => x.Id);
        b.Property(x => x.PolicyCode).HasMaxLength(100).IsRequired();
        b.Property(x => x.PolicyName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ItemType).HasConversion<int>();
        b.Property(x => x.TargetType).HasConversion<int>();
        b.Property(x => x.Version).IsRequired();
        b.Property(x => x.IsPublished).IsRequired();
        b.Property(x => x.Remark).HasMaxLength(1000);
        b.HasIndex(x => new { x.PolicyCode, x.Version }).IsUnique();
        b.HasIndex(x => new { x.ItemType, x.TargetType, x.IsPublished });
    }
}

public sealed class F03EndpointGovernancePolicyItemConfiguration : IEntityTypeConfiguration<F03EndpointGovernancePolicyItem>
{
    public void Configure(EntityTypeBuilder<F03EndpointGovernancePolicyItem> b)
    {
        b.ToTable("F03EndpointGovernancePolicyItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.ItemType).HasConversion<int>();
        b.Property(x => x.NormalizedName).HasMaxLength(255).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(255);
        b.Property(x => x.Publisher).HasMaxLength(255);
        b.Property(x => x.VersionConstraint).HasMaxLength(100);
        b.Property(x => x.Remark).HasMaxLength(1000);
        b.HasIndex(x => new { x.PolicyId, x.NormalizedName, x.ItemType }).IsUnique();
    }
}

public sealed class F03EndpointGovernanceRequestConfiguration : IEntityTypeConfiguration<F03EndpointGovernanceRequest>
{
    public void Configure(EntityTypeBuilder<F03EndpointGovernanceRequest> b)
    {
        b.ToTable("F03EndpointGovernanceRequests");
        b.HasKey(x => x.Id);
        b.Property(x => x.RequestType).HasConversion<int>();
        b.Property(x => x.EmployeeCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.DeptCode).HasMaxLength(20);
        b.Property(x => x.ItemName).HasMaxLength(255);
        b.Property(x => x.Publisher).HasMaxLength(255);
        b.Property(x => x.RequestedVersion).HasMaxLength(100);
        b.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        b.Property(x => x.SecurityReviewStatus).HasMaxLength(30).IsRequired();
        b.Property(x => x.WorkflowStatus).HasMaxLength(30).IsRequired();
        b.HasIndex(x => new { x.EndpointDeviceId, x.WorkflowStatus });
        b.HasIndex(x => new { x.EmployeeCode, x.RequestStatus });
    }
}

public sealed class F03EndpointComplianceFindingConfiguration : IEntityTypeConfiguration<F03EndpointComplianceFinding>
{
    public void Configure(EntityTypeBuilder<F03EndpointComplianceFinding> b)
    {
        b.ToTable("F03EndpointComplianceFindings");
        b.HasKey(x => x.Id);
        b.Property(x => x.ItemType).HasConversion<int>();
        b.Property(x => x.Result).HasConversion<int>();
        b.Property(x => x.ObservedName).HasMaxLength(255).IsRequired();
        b.Property(x => x.ObservedVersion).HasMaxLength(100);
        b.Property(x => x.FindingCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.FindingMessage).HasMaxLength(2000);
        b.HasIndex(x => new { x.EndpointDeviceId, x.PolicyId, x.PolicyVersion, x.EvaluatedAtUtc });
        b.HasIndex(x => new { x.EndpointDeviceId, x.Result, x.ObservedName });
    }
}
