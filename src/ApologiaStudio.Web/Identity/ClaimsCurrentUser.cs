using System.Security.Claims;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Web.Identity;

public sealed class ClaimsCurrentUser(IHttpContextAccessor httpContextAccessor)
    : ICurrentUser
{
    public UserId UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(
                ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId)
                ? new UserId(userId)
                : throw new InvalidOperationException(
                    "An authenticated user is required for this operation.");
        }
    }
}
