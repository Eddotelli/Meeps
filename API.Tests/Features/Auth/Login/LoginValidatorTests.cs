using API.Features.Auth.Login;
using FluentValidation.TestHelper;
using Shared.Contracts.Auth;
using Xunit;

namespace API.Tests.Features.Auth.Login;

/// <summary>
/// Unit tests for LoginValidator.
/// Tests validation rules for login requests.
/// </summary>
public class LoginValidatorTests
{
    private readonly LoginValidator _validator;

    public LoginValidatorTests()
    {
        _validator = new LoginValidator();
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Empty()
    {
        // Arrange
        var request = new LoginRequest { Email = "", Password = "password123" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("test@")]
    [InlineData("@test.com")]
    [InlineData("test")]
    public void Should_Have_Error_When_Email_Is_Invalid(string email)
    {
        // Arrange
        var request = new LoginRequest { Email = email, Password = "password123" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_Have_Error_When_Password_Is_Empty()
    {
        // Arrange
        var request = new LoginRequest { Email = "test@test.com", Password = "" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Valid_Request()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@test.com",
            Password = "ValidPassword123!"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("test.user@domain.co.uk")]
    [InlineData("user+tag@example.com")]
    public void Should_Accept_Valid_Email_Formats(string email)
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = email,
            Password = "password123"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }
}
