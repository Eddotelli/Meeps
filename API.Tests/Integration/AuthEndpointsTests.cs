using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Shared.Contracts.Auth;
using Xunit;

namespace API.Tests.Integration;

/// <summary>
/// Integration tests for authentication endpoints.
/// Tests the complete flow: Register → Verify Email → Complete Registration → Login
/// </summary>
public class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true // Enable cookie persistence for authentication
        });
    }

    [Fact]
    public async Task Register_Should_Return_Success_With_Valid_Email()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "newuser@test.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        result.Should().NotBeNull();
        result!.Message.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Register_Should_Return_BadRequest_With_Invalid_Email()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "invalid-email"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_Should_Return_Conflict_When_Email_Already_Exists()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "duplicate@test.com"
        };

        // Act - Register first time
        await _client.PostAsJsonAsync("/api/auth/register", request);

        // Act - Try to register again with same email
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_Should_Return_Unauthorized_With_Invalid_Credentials()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "notfound@test.com",
            Password = "wrongpassword"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CompleteRegistration_Should_Return_BadRequest_With_Invalid_Token()
    {
        // Arrange
        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "invalid-token",
            Password = "ValidPass123!",
            DisplayName = "TestUser",
            AcceptTerms = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/complete-registration", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Complete_Auth_Flow_Should_Work()
    {
        // Arrange
        var email = $"flow-test-{Guid.NewGuid()}@test.com";

        // 1. Register user
        var registerRequest = new RegisterRequest { Email = email };
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Get verification token using test endpoint
        var tokenResponse = await _client.GetAsync($"/api/test/verification-token/{email}");
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
        tokenData.Should().NotBeNull();
        tokenData!.Token.Should().NotBeNullOrEmpty();

        // 3. Verify email
        var verifyRequest = new VerifyEmailRequest(tokenData.Token);
        var verifyResponse = await _client.PostAsJsonAsync("/api/auth/verify-email", verifyRequest);
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Complete registration
        var completeRequest = new CompleteRegistrationRequest
        {
            VerificationToken = tokenData.Token,
            Password = "ValidPass123!",
            DisplayName = "FlowUser",
            AcceptTerms = true
        };
        var completeResponse = await _client.PostAsJsonAsync("/api/auth/complete-registration", completeRequest);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Login
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = "ValidPass123!"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        loginResult.Should().NotBeNull();
        loginResult!.Email.Should().Be(email);
        loginResult.DisplayName.Should().Be("FlowUser");
    }

    [Fact]
    public async Task Password_Reset_Flow_Should_Work()
    {
        // Arrange - Create and complete registration for a user
        var email = $"reset-test-{Guid.NewGuid()}@test.com";
        await RegisterAndCompleteUser(email, "ResetUser", "OldPassword123!");

        // 1. Request password reset
        var forgotRequest = new ForgotPasswordRequest { Email = email };
        var forgotResponse = await _client.PostAsJsonAsync("/api/auth/forgot-password", forgotRequest);
        forgotResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Get reset token using test endpoint
        var tokenResponse = await _client.GetAsync($"/api/test/reset-token/{email}");
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
        tokenData.Should().NotBeNull();
        tokenData!.Token.Should().NotBeNullOrEmpty();

        // 3. Reset password
        var resetRequest = new ResetPasswordRequest
        {
            Token = tokenData.Token,
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };
        var resetResponse = await _client.PostAsJsonAsync("/api/auth/reset-password", resetRequest);
        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Login with new password
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = "NewPassword123!"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        loginResult.Should().NotBeNull();
        loginResult!.Email.Should().Be(email);
    }

    [Fact]
    public async Task Password_Reset_Should_Fail_With_Old_Password_After_Reset()
    {
        // Arrange - Create user and reset password
        var email = $"reset-old-{Guid.NewGuid()}@test.com";
        await RegisterAndCompleteUser(email, "TestUser", "OldPassword123!");

        var forgotRequest = new ForgotPasswordRequest { Email = email };
        await _client.PostAsJsonAsync("/api/auth/forgot-password", forgotRequest);

        var tokenResponse = await _client.GetAsync($"/api/test/reset-token/{email}");
        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();

        var resetRequest = new ResetPasswordRequest
        {
            Token = tokenData!.Token,
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };
        await _client.PostAsJsonAsync("/api/auth/reset-password", resetRequest);

        // Act - Try to login with old password
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = "OldPassword123!" // Old password
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgotPassword_Should_Return_Success_For_NonExistent_Email()
    {
        // Arrange - Security: Always return success even if email doesn't exist
        var request = new ForgotPasswordRequest
        {
            Email = "nonexistent@test.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ForgotPasswordResponse>();
        result.Should().NotBeNull();
        result!.Message.Should().Contain("If the email exists");
    }

    #region Helper Methods

    private async Task RegisterAndCompleteUser(string email, string displayName, string password)
    {
        // 1. Register
        var registerRequest = new RegisterRequest { Email = email };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // 2. Get verification token
        var tokenResponse = await _client.GetAsync($"/api/test/verification-token/{email}");
        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();

        // 3. Verify email
        var verifyRequest = new VerifyEmailRequest(tokenData!.Token);
        await _client.PostAsJsonAsync("/api/auth/verify-email", verifyRequest);

        // 4. Complete registration
        var completeRequest = new CompleteRegistrationRequest
        {
            VerificationToken = tokenData.Token,
            Password = password,
            DisplayName = displayName,
            AcceptTerms = true
        };
        await _client.PostAsJsonAsync("/api/auth/complete-registration", completeRequest);
    }

    #endregion

    // Helper class for test endpoint response
    private class TokenResponse
    {
        public string Token { get; set; } = string.Empty;
    }
}
