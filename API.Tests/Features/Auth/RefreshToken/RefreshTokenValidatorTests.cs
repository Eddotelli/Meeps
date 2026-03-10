using API.Features.Auth.RefreshToken;
using FluentValidation.TestHelper;
using Shared.Contracts.Auth;
using Xunit;

namespace API.Tests.Features.Auth.RefreshToken;

/// <summary>
/// Unit tests for RefreshTokenValidator.
/// Tests validation rules for refresh token requests.
/// Note: RefreshToken is retrieved from HTTP-only cookie, not from request body.
/// This validator is minimal as the actual token validation happens in the handler.
/// </summary>
public class RefreshTokenValidatorTests
{
    private readonly RefreshTokenValidator _validator;

    public RefreshTokenValidatorTests()
    {
        _validator = new RefreshTokenValidator();
    }

    [Fact]
    public void Should_Pass_Validation_For_Empty_Request()
    {
        // Arrange
        var request = new RefreshTokenRequest();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        // RefreshTokenRequest is empty as token comes from cookie
        result.ShouldNotHaveAnyValidationErrors();
    }
}
