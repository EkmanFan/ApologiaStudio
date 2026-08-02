using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Application.Abstractions.Identity;

public interface ICurrentUser
{
    UserId UserId { get; }
}
