using API.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;

namespace API.Tests.Infrastructure.Services;

/// <summary>
/// Unit tests for LocationDetectionService.
/// Tests IP-based location detection and fallback logic.
/// </summary>
public class LocationDetectionServiceTests
{
    private readonly Mock<ILogger<LocationDetectionService>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;

    public LocationDetectionServiceTests()
    {
        _mockLogger = new Mock<ILogger<LocationDetectionService>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        _mockHttpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("::1")]
    [InlineData("127.0.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.1")]
    public async Task DetectLocationFromIP_Should_Return_Stockholm_For_Local_IPs(string? ipAddress)
    {
        // Arrange
        var service = new LocationDetectionService(_mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = await service.DetectLocationFromIP(ipAddress);

        // Assert
        result.Should().NotBeNull();
        result!.City.Should().Be("Stockholm");
        result.Country.Should().Be("Sweden");
        result.CountryCode.Should().Be("SE");
        result.Latitude.Should().Be(59.3293);
        result.Longitude.Should().Be(18.0686);
    }

    [Fact]
    public async Task DetectLocationFromIP_Should_Return_Stockholm_For_Non_Swedish_IP()
    {
        // Arrange
        var ipAddress = "8.8.8.8"; // Google DNS
        var responseContent = @"{
            ""status"": ""success"",
            ""country"": ""United States"",
            ""countryCode"": ""US"",
            ""city"": ""Mountain View"",
            ""lat"": 37.386,
            ""lon"": -122.0838
        }";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var service = new LocationDetectionService(_mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = await service.DetectLocationFromIP(ipAddress);

        // Assert
        result.Should().NotBeNull();
        result!.City.Should().Be("Stockholm");
        result.Country.Should().Be("Sweden");
    }

    [Fact]
    public async Task DetectLocationFromIP_Should_Return_Swedish_Location_For_Swedish_IP()
    {
        // Arrange
        var ipAddress = "194.71.0.1"; // Swedish IP
        var responseContent = @"{
            ""status"": ""success"",
            ""country"": ""Sweden"",
            ""countryCode"": ""SE"",
            ""city"": ""Gothenburg"",
            ""lat"": 57.7089,
            ""lon"": 11.9746
        }";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var service = new LocationDetectionService(_mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = await service.DetectLocationFromIP(ipAddress);

        // Assert
        result.Should().NotBeNull();
        result!.City.Should().Be("Gothenburg");
        result.Country.Should().Be("Sweden");
        result.CountryCode.Should().Be("SE");
        result.Latitude.Should().Be(57.7089);
        result.Longitude.Should().Be(11.9746);
        result.DisplayName.Should().Be("Gothenburg, Sweden");
    }

    [Fact]
    public async Task DetectLocationFromIP_Should_Return_Stockholm_When_API_Fails()
    {
        // Arrange
        var ipAddress = "8.8.8.8";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var service = new LocationDetectionService(_mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = await service.DetectLocationFromIP(ipAddress);

        // Assert
        result.Should().NotBeNull();
        result!.City.Should().Be("Stockholm");
        result.Country.Should().Be("Sweden");
    }

    [Fact]
    public async Task DetectLocationFromIP_Should_Return_Stockholm_When_API_Returns_Error_Status()
    {
        // Arrange
        var ipAddress = "8.8.8.8";
        var responseContent = @"{
            ""status"": ""fail"",
            ""message"": ""invalid query""
        }";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var service = new LocationDetectionService(_mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = await service.DetectLocationFromIP(ipAddress);

        // Assert
        result.Should().NotBeNull();
        result!.City.Should().Be("Stockholm");
    }

    [Fact]
    public async Task DetectLocationFromIP_Should_Return_Stockholm_When_Exception_Occurs()
    {
        // Arrange
        var ipAddress = "8.8.8.8";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var service = new LocationDetectionService(_mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = await service.DetectLocationFromIP(ipAddress);

        // Assert
        result.Should().NotBeNull();
        result!.City.Should().Be("Stockholm");
        result.Country.Should().Be("Sweden");
    }

    [Fact]
    public async Task DetectLocationFromIP_Should_Handle_Missing_City_In_Response()
    {
        // Arrange
        var ipAddress = "194.71.0.1";
        var responseContent = @"{
            ""status"": ""success"",
            ""country"": ""Sweden"",
            ""countryCode"": ""SE"",
            ""lat"": 59.3293,
            ""lon"": 18.0686
        }";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var service = new LocationDetectionService(_mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = await service.DetectLocationFromIP(ipAddress);

        // Assert
        result.Should().NotBeNull();
        result!.City.Should().Be("Stockholm");
        result.Country.Should().Be("Sweden");
    }
}
