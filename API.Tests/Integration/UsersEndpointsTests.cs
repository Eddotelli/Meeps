using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Shared.Contracts.Auth;
using Shared.Contracts.Users;
using Shared.Enums;
using Xunit;

namespace API.Tests.Integration;

/// <summary>
/// Integration tests for user endpoints.
/// Tests profile retrieval and user-related operations.
/// </summary>
public class UsersEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public UsersEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true // Enable cookie persistence for authentication
        });
    }

    [Fact]
    public async Task GetUserProfile_Should_Return_Unauthorized_When_Not_Authenticated()
    {
        // Act
        var response = await _client.GetAsync("/api/users/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserProfile_Should_Return_Profile_When_Authenticated()
    {
        // Arrange - Create and authenticate a user
        var email = $"profile-test-{Guid.NewGuid()}@test.com";
        var displayName = "ProfileTestUser";
        await RegisterAndAuthenticateUser(email, displayName);

        // Act
        var response = await _client.GetAsync("/api/users/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<GetUserProfileResponse>();
        profile.Should().NotBeNull();
        profile!.Email.Should().Be(email);
        profile.DisplayName.Should().Be(displayName);
        profile.Id.Should().BeGreaterThan(0);
        profile.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetUserProfile_Should_Return_Correct_Statistics()
    {
        // Arrange - Create and authenticate a user
        var email = $"stats-test-{Guid.NewGuid()}@test.com";
        await RegisterAndAuthenticateUser(email, "StatsTestUser");

        // Act
        var response = await _client.GetAsync("/api/users/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<GetUserProfileResponse>();
        profile.Should().NotBeNull();
        profile!.EventsCreated.Should().Be(0);
        profile.EventsJoined.Should().Be(0);
        profile.CategoriesCount.Should().Be(0);
    }

    [Fact]
    public async Task GetUserProfile_Should_Include_Optional_Fields()
    {
        // Arrange - Create and authenticate a user
        var email = $"optional-test-{Guid.NewGuid()}@test.com";
        await RegisterAndAuthenticateUser(email, "OptionalTestUser");

        // Act
        var response = await _client.GetAsync("/api/users/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<GetUserProfileResponse>();
        profile.Should().NotBeNull();
        profile!.Bio.Should().BeNullOrEmpty();
        profile.ProfileImageUrl.Should().BeNullOrEmpty();
        profile.Gender.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProfile_Should_Return_Unauthorized_When_Not_Authenticated()
    {
        // Arrange
        var request = new UpdateProfileRequest
        {
            DisplayName = "NewName"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/users/me", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_Should_Update_Profile_When_Authenticated()
    {
        // Arrange - Create and authenticate a user
        var email = $"update-test-{Guid.NewGuid()}@test.com";
        await RegisterAndAuthenticateUser(email, "OriginalName");

        var updateRequest = new UpdateProfileRequest
        {
            DisplayName = "UpdatedName",
            Bio = "Updated bio",
            Gender = Gender.Male,
            BirthDate = new DateTime(1995, 5, 15)
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/users/me", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<GetUserProfileResponse>();
        profile.Should().NotBeNull();
        profile!.DisplayName.Should().Be("UpdatedName");
        profile.Bio.Should().Be("Updated bio");
        profile.Gender.Should().Be(Gender.Male);
        profile.BirthDate.Should().Be(new DateTime(1995, 5, 15));
    }

    [Fact]
    public async Task UpdatePassword_Should_Return_Unauthorized_When_Not_Authenticated()
    {
        // Arrange
        var request = new UpdatePasswordRequest
        {
            CurrentPassword = "OldPass123!",
            NewPassword = "NewPass123!",
            ConfirmNewPassword = "NewPass123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/users/password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdatePassword_Should_Update_Password_When_Authenticated()
    {
        // Arrange - Create and authenticate a user
        var email = $"password-test-{Guid.NewGuid()}@test.com";
        await RegisterAndAuthenticateUser(email, "PasswordTestUser");

        var updateRequest = new UpdatePasswordRequest
        {
            CurrentPassword = "TestPass123!",
            NewPassword = "NewTestPass123!",
            ConfirmNewPassword = "NewTestPass123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/users/password", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify new password works by logging in
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = "NewTestPass123!"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdatePassword_Should_Fail_With_Wrong_Current_Password()
    {
        // Arrange - Create and authenticate a user
        var email = $"wrong-password-{Guid.NewGuid()}@test.com";
        await RegisterAndAuthenticateUser(email, "WrongPassUser");

        var updateRequest = new UpdatePasswordRequest
        {
            CurrentPassword = "WrongPassword123!",
            NewPassword = "NewTestPass123!",
            ConfirmNewPassword = "NewTestPass123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/users/password", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateEmail_Should_Return_Unauthorized_When_Not_Authenticated()
    {
        // Arrange
        var request = new UpdateEmailRequest
        {
            NewEmail = "newemail@test.com",
            Password = "Password123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/users/email", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateEmail_Should_Update_Email_When_Authenticated()
    {
        // Arrange - Create and authenticate a user
        var oldEmail = $"oldemail-{Guid.NewGuid()}@test.com";
        var newEmail = $"newemail-{Guid.NewGuid()}@test.com";
        await RegisterAndAuthenticateUser(oldEmail, "EmailTestUser");

        var updateRequest = new UpdateEmailRequest
        {
            NewEmail = newEmail,
            Password = "TestPass123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/users/email", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // NOTE: After email update, EmailConfirmed is set to false
        // User would need to verify the new email before logging in
        // This is correct security behavior - we're just verifying the update succeeded
    }

    #region Helper Methods

    /// <summary>
    /// Helper method to register and authenticate a test user.
    /// Returns the authentication cookies for subsequent requests.
    /// </summary>
    private async Task RegisterAndAuthenticateUser(string email, string displayName)
    {
        // 1. Register
        var registerRequest = new RegisterRequest { Email = email };
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Get verification token (test endpoint)
        var tokenResponse = await _client.GetAsync($"/api/test/verification-token/{email}");
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<TestTokenResponse>();
        var token = tokenData!.Token;

        // 3. Verify email
        var verifyRequest = new VerifyEmailRequest(Token: token);
        var verifyResponse = await _client.PostAsJsonAsync("/api/auth/verify-email", verifyRequest);
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Complete registration
        var completeRequest = new CompleteRegistrationRequest
        {
            VerificationToken = token,
            Password = "TestPass123!",
            DisplayName = displayName,
            BirthDate = new DateTime(1990, 1, 1),
            AcceptTerms = true
        };
        var completeResponse = await _client.PostAsJsonAsync("/api/auth/complete-registration", completeRequest);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Login
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = "TestPass123!"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// DTO for test endpoint response
    /// </summary>
    private class TestTokenResponse
    {
        public string Token { get; set; } = string.Empty;
    }

    #endregion
}
