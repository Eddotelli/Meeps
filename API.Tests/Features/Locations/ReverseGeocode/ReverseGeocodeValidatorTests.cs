using API.Features.Locations.ReverseGeocode;
using FluentValidation.TestHelper;
using Shared.Contracts.Locations;
using Xunit;

namespace API.Tests.Features.Locations.ReverseGeocode;

/// <summary>
/// Unit tests for ReverseGeocodeValidator.
/// Tests validation rules for reverse geocoding requests.
/// </summary>
public class ReverseGeocodeValidatorTests
{
    private readonly ReverseGeocodeValidator _validator;

    public ReverseGeocodeValidatorTests()
    {
        _validator = new ReverseGeocodeValidator();
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    [InlineData(-100)]
    [InlineData(100)]
    public void Should_Have_Error_When_Latitude_Is_Out_Of_Range(double latitude)
    {
        // Arrange
        var request = new ReverseGeocodeRequest
        {
            Latitude = latitude,
            Longitude = 0
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Latitude);
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    [InlineData(-200)]
    [InlineData(200)]
    public void Should_Have_Error_When_Longitude_Is_Out_Of_Range(double longitude)
    {
        // Arrange
        var request = new ReverseGeocodeRequest
        {
            Latitude = 0,
            Longitude = longitude
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Longitude);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(59.3293, 18.0686)] // Stockholm
    [InlineData(-33.8688, 151.2093)] // Sydney
    [InlineData(40.7128, -74.0060)] // New York
    [InlineData(90, 180)] // Max valid values
    [InlineData(-90, -180)] // Min valid values
    public void Should_Not_Have_Error_When_Valid_Coordinates(double latitude, double longitude)
    {
        // Arrange
        var request = new ReverseGeocodeRequest
        {
            Latitude = latitude,
            Longitude = longitude
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
