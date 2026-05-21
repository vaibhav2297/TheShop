namespace TheShop.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? Id { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
}
