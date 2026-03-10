using API.Features.Locations.SearchLocation;
using FluentValidation.TestHelper;
using Shared.Contracts.Locations;
using Xunit;

namespace API.Tests.Features.Locations.SearchLocation;

/// <summary>
/// Unit tests for SearchLocationValidator.
/// Tests validation rules for location search requests.
/// </summary>
public class SearchLocationValidatorTests
{
    private readonly SearchLocationValidator _validator;

    public SearchLocationValidatorTests()
    {
        _validator = new SearchLocationValidator();
    }

    [Fact]
    public void Should_Have_Error_When_Query_Is_Empty()
    {
        // Arrange
        var request = new SearchLocationRequest { Query = "" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Query);
    }

    [Fact]
    public void Should_Have_Error_When_Query_Is_Null()
    {
        // Arrange
        var request = new SearchLocationRequest { Query = null! };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Query);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("ab")]
    public void Should_Have_Error_When_Query_Is_Too_Short(string query)
    {
        // Arrange
        var request = new SearchLocationRequest { Query = query };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Query);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Valid_Query()
    {
        // Arrange
        var request = new SearchLocationRequest { Query = "Stockholm" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("Stockholm")]
    [InlineData("Stockholm, Sweden")]
    [InlineData("123 Main St")]
    public void Should_Accept_Various_Valid_Query_Formats(string query)
    {
        // Arrange
        var request = new SearchLocationRequest { Query = query };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
