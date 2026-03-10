using API.Features.Events.JoinEvent;
using FluentValidation.TestHelper;
using Shared.Contracts.Events;
using Xunit;

namespace API.Tests.Features.Events.JoinEvent;

/// <summary>
/// Unit tests for JoinEventValidator.
/// Tests validation rules for joining events.
/// </summary>
public class JoinEventValidatorTests
{
    private readonly JoinEventValidator _validator;

    public JoinEventValidatorTests()
    {
        _validator = new JoinEventValidator();
    }

    [Fact]
    public void Should_Not_Have_Any_Errors_When_Request_Is_Valid()
    {
        // Arrange
        var request = new JoinEventRequest { EventId = 1 };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_EventId_Is_Zero()
    {
        // Arrange
        var request = new JoinEventRequest { EventId = 0 };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EventId);
    }

    [Fact]
    public void Should_Have_Error_When_EventId_Is_Negative()
    {
        // Arrange
        var request = new JoinEventRequest { EventId = -1 };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EventId);
    }
}
