using FluentValidation.TestHelper;
using Shared.Contracts.Events;
using Xunit;

namespace API.Tests.Features.Events.GetEligibleEvents;

public class GetEligibleEventsValidatorTests
{
    private readonly API.Features.Events.GetEligibleEvents.GetEligibleEventsValidator _validator;

    public GetEligibleEventsValidatorTests()
    {
        _validator = new API.Features.Events.GetEligibleEvents.GetEligibleEventsValidator();
    }

    [Fact]
    public void Should_Not_Have_Error_When_Request_Is_Valid_With_Location()
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            Latitude = 59.3293,
            Longitude = 18.0686,
            RadiusKm = 50,
            PageNumber = 1,
            PageSize = 20,
            SortBy = "distance"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_Request_Is_Valid_Without_Location()
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            PageNumber = 1,
            PageSize = 20,
            SortBy = "date"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    [InlineData(1000)]
    public void Should_Have_Error_When_RadiusKm_Is_Out_Of_Range(int radius)
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            Latitude = 59.3293,
            Longitude = 18.0686,
            RadiusKm = radius,
            PageNumber = 1,
            PageSize = 20,
            SortBy = "distance"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RadiusKm);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(500)]
    public void Should_Not_Have_Error_When_RadiusKm_Is_Valid(int radius)
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            Latitude = 59.3293,
            Longitude = 18.0686,
            RadiusKm = radius,
            PageNumber = 1,
            PageSize = 20,
            SortBy = "distance"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.RadiusKm);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_Have_Error_When_CategoryId_Is_Zero_Or_Negative(int categoryId)
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            CategoryId = categoryId,
            PageNumber = 1,
            PageSize = 20,
            SortBy = "date"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CategoryId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    public void Should_Not_Have_Error_When_CategoryId_Is_Valid(int categoryId)
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            CategoryId = categoryId,
            PageNumber = 1,
            PageSize = 20,
            SortBy = "date"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.CategoryId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_Have_Error_When_PageNumber_Is_Zero_Or_Negative(int pageNumber)
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            PageNumber = pageNumber,
            PageSize = 20,
            SortBy = "date"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageNumber);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Should_Not_Have_Error_When_PageNumber_Is_Valid(int pageNumber)
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            PageNumber = pageNumber,
            PageSize = 20,
            SortBy = "date"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PageNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Should_Have_Error_When_PageSize_Is_Out_Of_Range(int pageSize)
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            PageNumber = 1,
            PageSize = pageSize,
            SortBy = "date"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(100)]
    public void Should_Not_Have_Error_When_PageSize_Is_Valid(int pageSize)
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            PageNumber = 1,
            PageSize = pageSize,
            SortBy = "date"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData("distance")]
    [InlineData("date")]
    [InlineData("name")]
    public void Should_Not_Have_Error_When_SortBy_Is_Valid(string sortBy)
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            PageNumber = 1,
            PageSize = 20,
            SortBy = sortBy
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.SortBy);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("time")]
    [InlineData("popularity")]
    public void Should_Have_Error_When_SortBy_Is_Invalid(string sortBy)
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            PageNumber = 1,
            PageSize = 20,
            SortBy = sortBy
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void Should_Have_Error_When_Latitude_Is_Out_Of_Range(double latitude)
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            Latitude = latitude,
            Longitude = 18.0686,
            PageNumber = 1,
            PageSize = 20,
            SortBy = "distance"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Latitude);
    }

    [Theory]
    [InlineData(-90)]
    [InlineData(0)]
    [InlineData(90)]
    public void Should_Not_Have_Error_When_Latitude_Is_Valid(double latitude)
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            Latitude = latitude,
            Longitude = 18.0686,
            PageNumber = 1,
            PageSize = 20,
            SortBy = "distance"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Latitude);
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public void Should_Have_Error_When_Longitude_Is_Out_Of_Range(double longitude)
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            Latitude = 59.3293,
            Longitude = longitude,
            PageNumber = 1,
            PageSize = 20,
            SortBy = "distance"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Longitude);
    }

    [Theory]
    [InlineData(-180)]
    [InlineData(0)]
    [InlineData(180)]
    public void Should_Not_Have_Error_When_Longitude_Is_Valid(double longitude)
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            Latitude = 59.3293,
            Longitude = longitude,
            PageNumber = 1,
            PageSize = 20,
            SortBy = "distance"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Longitude);
    }

    [Fact]
    public void Should_Have_Error_When_Latitude_Is_Provided_Without_Longitude()
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            Latitude = 59.3293,
            Longitude = null,
            PageNumber = 1,
            PageSize = 20,
            SortBy = "distance"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Longitude);
    }

    [Fact]
    public void Should_Have_Error_When_Longitude_Is_Provided_Without_Latitude()
    {
        // Arrange
        var request = new GetEligibleEventsRequest
        {
            Latitude = null,
            Longitude = 18.0686,
            PageNumber = 1,
            PageSize = 20,
            SortBy = "distance"
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Latitude);
    }
}
