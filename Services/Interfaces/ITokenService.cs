using PersonalFinanceTracker.Api.Entities;

namespace PersonalFinanceTracker.Api.Services.Interfaces;

public interface ITokenService
{
    string CreateAccessToken(User user);
    RefreshToken CreateRefreshToken(Guid userId);
}