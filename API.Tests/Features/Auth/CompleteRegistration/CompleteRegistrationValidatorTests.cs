using API.Features.Auth.CompleteRegistration;
using FluentValidation.TestHelper;
using Shared.Contracts.Auth;
using Xunit;

namespace API.Tests.Features.Auth.CompleteRegistration;

/// <summary>
/// Unit tests for CompleteRegistrationValidator.
/// Tests password complexity, confirmation matching, and other validation rules.
/// </summary>
public class CompleteRegistrationValidatorTests
{
    private readonly CompleteRegistrationValidator _validator;

    public CompleteRegistrationValidatorTests()
    {
        _validator = new CompleteRegistrationValidator();
    }

    #region VerificationToken Tests

    [Fact]
    public void Should_Have_Error_When_VerificationToken_Is_Empty()
    {
        // Arrange
        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "",
            Password = "ValidPass123!",
            DisplayName = "TestUser",
            AcceptTerms = true
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.VerificationToken);
    }

    #endregion

    #region Password Tests

    [Fact]
    public void Should_Have_Error_When_Password_Is_Empty()
    {
        // Arrange
        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            Password = "",
            DisplayName = "TestUser",
            AcceptTerms = true
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("1234567")]
    public void Should_Have_Error_When_Password_Is_Too_Short(string password)
    {
        // Arrange
        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            Password = password,
            DisplayName = "TestUser",
            AcceptTerms = true
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("alllowercase123!")] // No uppercase
    [InlineData("ALLUPPERCASE123!")] // No lowercase
    [InlineData("NoNumbersHere!")]   // No numbers
    [InlineData("NoSpecialChar123")]  // No special characters
    public void Should_Have_Error_When_Password_Missing_Required_Characters(string password)
    {
        // Arrange
        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            Password = password,
            DisplayName = "TestUser",
            AcceptTerms = true
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("ValidPass123!")]
    [InlineData("Str0ng@Pass")]
    [InlineData("MyP@ssw0rd")]
    public void Should_Not_Have_Error_When_Password_Is_Valid(string password)
    {
        // Arrange
        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            Password = password,
            DisplayName = "TestUser",
            AcceptTerms = true
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    #endregion

    #region AcceptTerms Tests

    [Fact]
    public void Should_Have_Error_When_AcceptTerms_Is_False()
    {
        // Arrange
        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            Password = "ValidPass123!",
            DisplayName = "TestUser",
            AcceptTerms = false
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AcceptTerms);
    }

    [Fact]
    public void Should_Not_Have_Error_When_AcceptTerms_Is_True()
    {
        // Arrange
        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            Password = "ValidPass123!",
            DisplayName = "TestUser",
            AcceptTerms = true
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.AcceptTerms);
    }

    #endregion

    #region DisplayName Tests

    [Fact]
    public void Should_Have_Error_When_DisplayName_Is_Empty()
    {
        // Arrange
        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            Password = "ValidPass123!",
            DisplayName = "",
            AcceptTerms = true
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("x")]
    public void Should_Have_Error_When_DisplayName_Is_Too_Short(string displayName)
    {
        // Arrange
        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            Password = "ValidPass123!",
            DisplayName = displayName,
            AcceptTerms = true
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    #endregion

    #region Complete Valid Request

    [Fact]
    public void Should_Not_Have_Any_Errors_When_Request_Is_Valid()
    {
        // Arrange
        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token-12345",
            Password = "ValidPass123!",
            DisplayName = "TestUser",
            AcceptTerms = true,
            BirthDate = new DateTime(1990, 5, 15)
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
