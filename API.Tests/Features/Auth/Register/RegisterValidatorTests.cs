using API.Features.Auth.Register;
using FluentValidation.TestHelper;
using Shared.Contracts.Auth;
using Xunit;

namespace API.Tests.Features.Auth.Register;

/// <summary>
/// Unit tests for RegisterValidator.
/// Tests validation rules for registration requests.
/// </summary>
public class RegisterValidatorTests
{
    private readonly RegisterValidator _validator;

    public RegisterValidatorTests()
    {
        _validator = new RegisterValidator();
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Empty()
    {
        // Arrange
        var request = new RegisterRequest { Email = "" };

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
    [InlineData("test.com")]
    public void Should_Have_Error_When_Email_Is_Invalid(string email)
    {
        // Arrange
        var request = new RegisterRequest { Email = email };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_Have_Error_When_Email_Exceeds_MaxLength()
    {
        // Arrange
        var longEmail = new string('a', 250) + "@test.com"; // Over 256 chars
        var request = new RegisterRequest { Email = longEmail };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Valid_Email()
    {
        // Arrange
        var request = new RegisterRequest { Email = "test@test.com" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("test.user@domain.co.uk")]
    [InlineData("user+tag@example.com")]
    public void Should_Accept_Various_Valid_Email_Formats(string email)
    {
        // Arrange
        var request = new RegisterRequest { Email = email };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
