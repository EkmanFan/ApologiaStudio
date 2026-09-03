using ApologiaStudio.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class ApologiaIdentityUserConfiguration
    : IEntityTypeConfiguration<ApologiaIdentityUser>
{
    public void Configure(EntityTypeBuilder<ApologiaIdentityUser> builder)
    {
        builder.Property(user => user.DisplayName)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(user => user.RegistrationStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(user => user.RegisteredAtUtc)
            .IsRequired();
        builder.Property(user => user.RejectionReason)
            .HasMaxLength(2000);
        builder.HasIndex(user => user.RegistrationStatus);
        builder.HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique();
    }
}

internal sealed class IdentityAdministrationEventConfiguration
    : IEntityTypeConfiguration<IdentityAdministrationEventEntity>
{
    public void Configure(
        EntityTypeBuilder<IdentityAdministrationEventEntity> builder)
    {
        builder.ToTable("identity_administration_events");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id)
            .UseIdentityByDefaultColumn();
        builder.Property(entity => entity.Action)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(entity => entity.Reason)
            .HasMaxLength(2000);
        builder.Property(entity => entity.Details)
            .HasMaxLength(4000);
        builder.HasIndex(entity => new
        {
            entity.TargetUserId,
            entity.OccurredAtUtc
        });
        builder.HasOne<ApologiaIdentityUser>()
            .WithMany()
            .HasForeignKey(entity => entity.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IdentityGroupEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.TargetGroupId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
            .WithMany()
            .HasForeignKey(entity => entity.TargetRoleId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ApologiaIdentityUser>()
            .WithMany()
            .HasForeignKey(entity => entity.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class IdentityGroupConfiguration
    : IEntityTypeConfiguration<IdentityGroupEntity>
{
    public void Configure(EntityTypeBuilder<IdentityGroupEntity> builder)
    {
        builder.ToTable("identity_groups");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name)
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(entity => entity.NormalizedName)
            .HasMaxLength(120)
            .IsRequired();
        builder.Property(entity => entity.Description)
            .HasMaxLength(500);
        builder.HasIndex(entity => entity.NormalizedName)
            .IsUnique();
        builder.HasOne<ApologiaIdentityUser>()
            .WithMany()
            .HasForeignKey(entity => entity.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class IdentityGroupMembershipConfiguration
    : IEntityTypeConfiguration<IdentityGroupMembershipEntity>
{
    public void Configure(
        EntityTypeBuilder<IdentityGroupMembershipEntity> builder)
    {
        builder.ToTable("identity_group_memberships");
        builder.HasKey(entity => new { entity.GroupId, entity.UserId });
        builder.HasOne<IdentityGroupEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApologiaIdentityUser>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApologiaIdentityUser>()
            .WithMany()
            .HasForeignKey(entity => entity.AddedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class IdentityGroupRoleConfiguration
    : IEntityTypeConfiguration<IdentityGroupRoleEntity>
{
    public void Configure(EntityTypeBuilder<IdentityGroupRoleEntity> builder)
    {
        builder.ToTable("identity_group_roles");
        builder.HasKey(entity => new { entity.GroupId, entity.RoleId });
        builder.HasOne<IdentityGroupEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
            .WithMany()
            .HasForeignKey(entity => entity.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApologiaIdentityUser>()
            .WithMany()
            .HasForeignKey(entity => entity.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
