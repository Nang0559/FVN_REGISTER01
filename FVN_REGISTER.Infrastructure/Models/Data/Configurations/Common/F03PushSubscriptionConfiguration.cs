using FVN_REGISTER.Core.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FVN_REGISTER.Infrastructure.Models.Data.Configurations.Common
{
    public class F03PushSubscriptionConfiguration : IEntityTypeConfiguration<F03PushSubscription>
    {
        public void Configure(EntityTypeBuilder<F03PushSubscription> entity)
        {
            entity.ToTable("F03PushSubscriptions");
            entity.HasKey(e => e.Id).HasName("PK_F03PushSubscriptions");

            // char(64) must match the SQL column, otherwise EF sends nvarchar and SQL Server
            // converts the column side (implicit conversion) which prevents an index seek.
            entity.Property(e => e.EndpointHash).HasColumnType("char(64)").IsRequired();
            entity.Property(e => e.Endpoint).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.P256dh).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Auth).HasMaxLength(100).IsRequired();
            entity.Property(e => e.UserAgent).HasMaxLength(300);

            entity.HasIndex(e => e.EndpointHash, "UX_F03PushSubscriptions_EndpointHash").IsUnique();
            entity.HasIndex(e => e.UserId, "IX_F03PushSubscriptions_User");
        }
    }
}
