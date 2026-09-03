using ApologiaStudio.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace ApologiaStudio.Infrastructure.Persistence.Identity;

public sealed class ApologiaIdentityUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public AccountRegistrationStatus RegistrationStatus { get; set; } =
        AccountRegistrationStatus.PendingEmail;

    public DateTimeOffset RegisteredAtUtc { get; set; }

    public DateTimeOffset? EmailVerifiedAtUtc { get; set; }

    public DateTimeOffset? ReviewedAtUtc { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public string? RejectionReason { get; set; }
}

public sealed class IdentityAdministrationEventEntity
{
    public long Id { get; set; }

    public Guid? TargetUserId { get; set; }

    public Guid? TargetGroupId { get; set; }

    public Guid? TargetRoleId { get; set; }

    public Guid ActorUserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string? Details { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}

public sealed class IdentityGroupEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }
}

public sealed class IdentityGroupMembershipEntity
{
    public Guid GroupId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset AddedAtUtc { get; set; }

    public Guid AddedByUserId { get; set; }
}

public sealed class IdentityGroupRoleEntity
{
    public Guid GroupId { get; set; }

    public Guid RoleId { get; set; }

    public DateTimeOffset AssignedAtUtc { get; set; }

    public Guid? AssignedByUserId { get; set; }
}
