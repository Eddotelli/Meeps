using API.Models;

namespace API.Infrastructure.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user, IList<string> roles);
    string GenerateRefreshToken();
    string GenerateEmailVerificationToken();
}
