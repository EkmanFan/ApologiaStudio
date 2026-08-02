using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Web.Identity;

public sealed class DemoCurrentUser : ICurrentUser
{
    public UserId UserId { get; } = new(
        Guid.Parse(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
}
