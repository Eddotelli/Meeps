# Meeps Testing Guidelines

## Overview

This document provides comprehensive guidelines for writing tests in the Meeps application. Use this as a reference when creating new tests or when prompting AI assistants to generate tests.

**Last Updated:** January 2026  
**Test Frameworks:** xUnit, bUnit, FluentValidation.TestHelper, Moq, FluentAssertions

---

## Table of Contents

1. [Testing Philosophy](#testing-philosophy)
2. [Test Project Structure](#test-project-structure)
3. [Unit Tests - API Validators](#unit-tests---api-validators)
4. [Unit Tests - API Handlers](#unit-tests---api-handlers)
5. [Integration Tests - API Endpoints](#integration-tests---api-endpoints)
6. [Unit Tests - Client Components](#unit-tests---client-components)
7. [Unit Tests - Shared Contracts](#unit-tests---shared-contracts)
8. [Postman/Newman API Tests](#postmannewman-api-tests)
9. [Test Naming Conventions](#test-naming-conventions)
10. [Common Patterns & Best Practices](#common-patterns--best-practices)
11. [Code Coverage Goals](#code-coverage-goals)
12. [Running Tests](#running-tests)
13. [Troubleshooting](#troubleshooting)
14. [AI Prompt Templates](#ai-prompt-templates)

---

## Testing Philosophy

### Test Pyramid

```
     /\
    /E2E\         ← Few (Postman collections, manual testing)
   /------\
  /Integration\   ← Some (API endpoints with database)
 /------------\
/Unit Tests    \  ← Many (Validators, Services, Components)
```

### Key Principles

1. **Test Behavior, Not Implementation** - Test what the code does, not how it does it
2. **Arrange-Act-Assert (AAA)** - Structure all tests consistently
3. **One Concept Per Test** - Each test should verify one thing
4. **Fast and Isolated** - Tests should run quickly and independently
5. **Readable Test Names** - Test names should describe what they test

### When to Write Tests

- ✅ **ALWAYS** test validators (100% coverage required)
- ✅ **ALWAYS** test business logic in handlers
- ✅ **USUALLY** test API endpoints (integration tests)
- ✅ **USUALLY** test complex UI components
- ❌ **RARELY** test simple DTOs without logic
- ❌ **NEVER** test third-party libraries

---

## Test Project Structure

```
Meeps/
├── API.Tests/
│   ├── Features/                      # Mirrors API/Features structure
│   │   └── Auth/
│   │       ├── Login/
│   │       │   ├── LoginValidatorTests.cs
│   │       │   └── LoginHandlerTests.cs
│   │       └── CompleteRegistration/
│   │           ├── CompleteRegistrationValidatorTests.cs
│   │           └── CompleteRegistrationHandlerTests.cs
│   ├── Integration/
│   │   ├── CustomWebApplicationFactory.cs
│   │   └── AuthEndpointsTests.cs
│   └── Helpers/
│       └── TestData.cs (if needed)
│
├── Client.Tests/
│   ├── Components/
│   │   └── Auth/
│   │       ├── LoginFormTests.cs
│   │       └── RegisterFormTests.cs
│   ├── Services/
│   │   └── I18nServiceTests.cs
│   └── Pages/
│       └── Auth/
│           └── LoginTests.cs
│
├── Shared.Tests/
│   └── Contracts/
│       └── Auth/
│           └── LoginRequestTests.cs
│
└── Tests/
    └── Postman/
        ├── README.md
        ├── meeps-api.postman_collection.json
        └── meeps-local.postman_environment.json
```

---

## Unit Tests - API Validators

### Purpose

Test FluentValidation validators to ensure all validation rules are correct. These are **critical** for API security.

### Template

```csharp
using FluentValidation.TestHelper;
using Shared.Contracts.{Feature};
using Xunit;

namespace API.Tests.Features.{Feature}.{Action};

/// <summary>
/// Unit tests for {Action}Validator.
/// Tests all validation rules for {description}.
/// </summary>
public class {Action}ValidatorTests
{
    private readonly {Action}Validator _validator;

    public {Action}ValidatorTests()
    {
        _validator = new {Action}Validator();
    }

    #region {PropertyName} Tests

    [Fact]
    public void Should_Have_Error_When_{PropertyName}_Is_Empty()
    {
        // Arrange
        var request = new {Action}Request
        {
            {PropertyName} = "" // or default value that should fail
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.{PropertyName});
    }

    [Theory]
    [InlineData({invalid_value_1})]
    [InlineData({invalid_value_2})]
    public void Should_Have_Error_When_{PropertyName}_Is_Invalid(string value)
    {
        // Arrange
        var request = new {Action}Request { {PropertyName} = value };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.{PropertyName});
    }

    [Theory]
    [InlineData({valid_value_1})]
    [InlineData({valid_value_2})]
    public void Should_Not_Have_Error_When_{PropertyName}_Is_Valid(string value)
    {
        // Arrange
        var request = new {Action}Request
        {
            {PropertyName} = value,
            // ... other required properties with valid values
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.{PropertyName});
    }

    #endregion

    #region Complete Valid Request

    [Fact]
    public void Should_Not_Have_Any_Errors_When_Request_Is_Valid()
    {
        // Arrange
        var request = new {Action}Request
        {
            // All properties with valid values
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
```

### What to Test

For each property in the DTO:

- ✅ Required validation (empty, null)
- ✅ Length validation (MinLength, MaxLength, StringLength)
- ✅ Format validation (Email, RegularExpression, etc.)
- ✅ Range validation (Min, Max, InclusiveBetween)
- ✅ Custom validation rules
- ✅ Comparison validation (Equal, NotEqual)
- ✅ At least one test with a completely valid request

### Example: Password Validation Tests

```csharp
[Theory]
[InlineData("short")]              // Too short
[InlineData("1234567")]            // No letters
[InlineData("alllowercase123!")]   // No uppercase
[InlineData("ALLUPPERCASE123!")]   // No lowercase
[InlineData("NoNumbers!")]         // No numbers
[InlineData("NoSpecialChar123")]   // No special characters
public void Should_Have_Error_When_Password_Invalid(string password)
{
    var request = new CompleteRegistrationRequest { Password = password };
    var result = _validator.TestValidate(request);
    result.ShouldHaveValidationErrorFor(x => x.Password);
}

[Theory]
[InlineData("ValidPass123!")]
[InlineData("Str0ng@Pass")]
[InlineData("MyP@ssw0rd")]
public void Should_Not_Have_Error_When_Password_Is_Valid(string password)
{
    var request = new CompleteRegistrationRequest
    {
        Password = password,
        ConfirmPassword = password,
        // ... other required fields
    };
    var result = _validator.TestValidate(request);
    result.ShouldNotHaveValidationErrorFor(x => x.Password);
}
```

---

## Unit Tests - API Handlers

### Purpose

Test business logic in handlers without hitting the database or external services.

### Template

```csharp
using API.Features.{Feature}.{Action};
using API.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shared.Contracts.{Feature};
using Xunit;

namespace API.Tests.Features.{Feature}.{Action};

/// <summary>
/// Unit tests for {Action}Handler.
/// Tests business logic for {description}.
/// </summary>
public class {Action}HandlerTests
{
    private readonly Mock<ApplicationDbContext> _mockContext;
    private readonly Mock<I{ServiceName}> _mock{ServiceName};
    private readonly {Action}Handler _handler;

    public {Action}HandlerTests()
    {
        // Setup mocks
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _mockContext = new Mock<ApplicationDbContext>(options);
        _mock{ServiceName} = new Mock<I{ServiceName}>();

        // Create handler with mocked dependencies
        _handler = new {Action}Handler(_mockContext.Object, _mock{ServiceName}.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_When_{Condition}()
    {
        // Arrange
        var request = new {Action}Request
        {
            // Valid request data
        };

        // Setup mocks
        _mock{ServiceName}
            .Setup(x => x.MethodName(It.IsAny<string>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.PropertyName.Should().Be(expectedValue);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_{Condition}()
    {
        // Arrange
        var request = new {Action}Request { /* data */ };

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be({Feature}Errors.{ErrorName}.Code);
    }

    [Fact]
    public async Task Handle_Should_Call_{ServiceName}_Once()
    {
        // Arrange
        var request = new {Action}Request { /* data */ };

        // Act
        await _handler.Handle(request);

        // Assert
        _mock{ServiceName}.Verify(
            x => x.MethodName(It.Is<string>(s => s == expectedValue)),
            Times.Once);
    }
}
```

### What to Test

- ✅ Success scenarios (happy path)
- ✅ Failure scenarios (all error cases)
- ✅ Edge cases (null, empty, boundary values)
- ✅ Service calls (verify mocks were called correctly)
- ✅ Database operations (add, update, delete)
- ✅ Business rules enforcement

### Example: Testing Database Operations

```csharp
[Fact]
public async Task Handle_Should_Add_Entity_To_Database()
{
    // Arrange
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    using var context = new ApplicationDbContext(options);
    var handler = new CreateEventHandler(context);

    var request = new CreateEventRequest { Title = "Test Event" };

    // Act
    var result = await handler.Handle(request);

    // Assert
    result.IsSuccess.Should().BeTrue();
    context.Events.Should().HaveCount(1);
    context.Events.First().Title.Should().Be("Test Event");
}
```

---

## Integration Tests - API Endpoints

### Purpose

Test complete HTTP request/response flow with a real (in-memory) database.

### Template

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Shared.Contracts.{Feature};
using Xunit;

namespace API.Tests.Integration;

/// <summary>
/// Integration tests for {Feature} endpoints.
/// Tests complete HTTP flow with database.
/// </summary>
public class {Feature}EndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public {Feature}EndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task {Action}_Should_Return_Success_With_Valid_Data()
    {
        // Arrange
        var request = new {Action}Request
        {
            // Valid data
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/{endpoint}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<{Action}Response>();
        result.Should().NotBeNull();
        result!.PropertyName.Should().NotBeEmpty();
    }

    [Fact]
    public async Task {Action}_Should_Return_BadRequest_With_Invalid_Data()
    {
        // Arrange
        var request = new {Action}Request { /* invalid data */ };

        // Act
        var response = await _client.PostAsJsonAsync("/api/{endpoint}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task {Action}_Should_Return_Unauthorized_Without_Token()
    {
        // Arrange
        var request = new {Action}Request { /* data */ };

        // Act
        var response = await _client.PostAsJsonAsync("/api/{endpoint}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

### What to Test

- ✅ Success responses (200, 201)
- ✅ Validation errors (400)
- ✅ Authentication/Authorization (401, 403)
- ✅ Not found (404)
- ✅ Conflict (409)
- ✅ Server errors (500)
- ✅ Complete user flows (multi-step processes)

### Example: Testing with Authentication

```csharp
[Fact]
public async Task CreateEvent_Should_Require_Authentication()
{
    // Arrange
    var request = new CreateEventRequest { Title = "Test Event" };

    // Act - No Authorization header
    var response = await _client.PostAsJsonAsync("/api/events", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

[Fact]
public async Task CreateEvent_Should_Work_With_Valid_Token()
{
    // Arrange
    // 1. First login to get token
    var loginRequest = new LoginRequest { Email = "test@test.com", Password = "Pass123!" };
    var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
    var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

    // 2. Add Authorization header
    _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {loginResult!.AccessToken}");

    var request = new CreateEventRequest { Title = "Test Event" };

    // Act
    var response = await _client.PostAsJsonAsync("/api/events", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);
}
```

---

## Unit Tests - Client Components

### Purpose

Test Blazor components using bUnit, including rendering, user interactions, and API calls.

### Template

```csharp
using Bunit;
using Client.Components.{Feature};
using Client.Services;
using Client.Services.ApiClients;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Common.Results;
using Shared.Contracts.{Feature};
using Xunit;

namespace Client.Tests.Components.{Feature};

/// <summary>
/// Unit tests for {ComponentName} component.
/// Tests rendering, user interaction, and API integration.
/// </summary>
public class {ComponentName}Tests : TestContext
{
    private readonly Mock<{Feature}ApiClient> _mockApiClient;
    private readonly Mock<I18nService> _mockI18nService;

    public {ComponentName}Tests()
    {
        _mockApiClient = new Mock<{Feature}ApiClient>(null!);
        _mockI18nService = new Mock<I18nService>(null!);

        // Setup I18n to return keys
        _mockI18nService
            .Setup(x => x.GetCommon(It.IsAny<string>()))
            .Returns<string>(key => key);
        _mockI18nService
            .Setup(x => x.GetValidation(It.IsAny<string>()))
            .Returns<string>(key => key);

        Services.AddSingleton(_mockApiClient.Object);
        Services.AddSingleton(_mockI18nService.Object);
    }

    [Fact]
    public void Should_Render_{Elements}()
    {
        // Act
        var cut = RenderComponent<{ComponentName}>();

        // Assert
        var element = cut.Find("selector");
        element.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_Call_API_When_{Action}()
    {
        // Arrange
        _mockApiClient
            .Setup(x => x.Method(It.IsAny<Request>()))
            .ReturnsAsync(Result<Response>.Success(new Response()));

        var cut = RenderComponent<{ComponentName}>();

        // Act
        var button = cut.Find("button");
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        _mockApiClient.Verify(x => x.Method(It.IsAny<Request>()), Times.Once);
    }

    [Fact]
    public async Task Should_Display_Error_When_API_Fails()
    {
        // Arrange
        _mockApiClient
            .Setup(x => x.Method(It.IsAny<Request>()))
            .ReturnsAsync(Result<Response>.Failure(new Error("Code", "Message")));

        var cut = RenderComponent<{ComponentName}>();

        // Act
        // Trigger action

        // Assert
        var alert = cut.Find(".mud-alert-error");
        alert.Should().NotBeNull();
    }
}
```

### What to Test

- ✅ Component renders correctly
- ✅ Form inputs work
- ✅ Validation messages display
- ✅ Button clicks trigger correct actions
- ✅ API calls are made with correct data
- ✅ Success/error messages display
- ✅ Loading states work
- ❌ Don't test MudBlazor internals
- ❌ Don't test CSS/styling

---

## Unit Tests - Shared Contracts

### Purpose

Test DataAnnotations validation on DTOs (client-side validation).

### Template

```csharp
using FluentAssertions;
using Shared.Contracts.{Feature};
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Shared.Tests.Contracts.{Feature};

/// <summary>
/// Tests DataAnnotations validation on {Request}Request DTO.
/// These are client-side validation rules.
/// </summary>
public class {Request}RequestTests
{
    [Fact]
    public void Should_Have_Required_{PropertyName}()
    {
        // Arrange
        var request = new {Request}Request { {PropertyName} = "" };

        // Act
        var validationResults = ValidateModel(request);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains(nameof({Request}Request.{PropertyName})));
    }

    [Fact]
    public void Should_Pass_Validation_When_Valid()
    {
        // Arrange
        var request = new {Request}Request
        {
            // All valid properties
        };

        // Act
        var validationResults = ValidateModel(request);

        // Assert
        validationResults.Should().BeEmpty();
    }

    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }
}
```

---

## Postman/Newman API Tests

### Structure

```json
{
  "name": "Feature Name",
  "item": [
    {
      "name": "Action Name",
      "event": [
        {
          "listen": "test",
          "script": {
            "exec": [
              "pm.test(\"Status is 200 OK\", function () {",
              "    pm.response.to.have.status(200);",
              "});",
              "",
              "pm.test(\"Response has expected property\", function () {",
              "    var jsonData = pm.response.json();",
              "    pm.expect(jsonData.propertyName).to.exist;",
              "});"
            ]
          }
        }
      ],
      "request": {
        "method": "POST",
        "url": "{{baseUrl}}/api/endpoint"
      }
    }
  ]
}
```

### Test Scripts to Include

```javascript
// Status Code
pm.test("Status is 200 OK", function () {
  pm.response.to.have.status(200);
});

// Response Time
pm.test("Response time is less than 500ms", function () {
  pm.expect(pm.response.responseTime).to.be.below(500);
});

// JSON Structure
pm.test("Response has expected property", function () {
  var jsonData = pm.response.json();
  pm.expect(jsonData.propertyName).to.exist;
  pm.expect(jsonData.propertyName).to.be.a("string");
});

// Save to Environment
pm.test("Save token to environment", function () {
  var jsonData = pm.response.json();
  pm.environment.set("accessToken", jsonData.accessToken);
});

// Cookies
pm.test("Has refresh token cookie", function () {
  pm.expect(pm.cookies.has("refreshToken")).to.be.true;
});
```

---

## Test Naming Conventions

### Method Names

Format: `Should_{ExpectedBehavior}_When_{Condition}`

Examples:

```csharp
Should_Have_Error_When_Email_Is_Empty
Should_Return_Success_When_Valid_Request
Should_Call_EmailService_Once_When_Register
Should_Display_ValidationError_When_Password_Invalid
```

### Class Names

Format: `{ClassName}Tests`

Examples:

```csharp
LoginValidatorTests
CreateEventHandlerTests
AuthEndpointsTests
LoginFormTests
```

### Test Categories

Use `#region` for grouping related tests:

```csharp
#region Password Tests
// Password validation tests here
#endregion

#region Email Tests
// Email validation tests here
#endregion
```

---

## Common Patterns & Best Practices

### AAA Pattern

```csharp
[Fact]
public async Task TestName()
{
    // Arrange - Setup test data and mocks
    var request = new Request { /* data */ };
    _mockService.Setup(/* mock setup */);

    // Act - Execute the method being tested
    var result = await _handler.Handle(request);

    // Assert - Verify the outcome
    result.IsSuccess.Should().BeTrue();
}
```

### Theory with InlineData

```csharp
[Theory]
[InlineData("value1")]
[InlineData("value2")]
[InlineData("value3")]
public void TestName(string input)
{
    // Test with multiple inputs
}
```

### Testing Async Methods

```csharp
[Fact]
public async Task Should_Work_Async()
{
    // Always use async/await for async tests
    var result = await _handler.Handle(request);
    result.Should().NotBeNull();
}
```

### Mocking Setup

```csharp
// Return value
_mockService.Setup(x => x.Method(It.IsAny<string>()))
    .ReturnsAsync(value);

// Verify call
_mockService.Verify(
    x => x.Method(It.Is<string>(s => s == expected)),
    Times.Once);

// Match specific argument
_mockService.Setup(x => x.Method(It.Is<int>(i => i > 0)))
    .ReturnsAsync(value);
```

### FluentAssertions

```csharp
// Boolean
result.IsSuccess.Should().BeTrue();
result.IsFailure.Should().BeFalse();

// Equality
actual.Should().Be(expected);
actual.Should().NotBe(unexpected);

// Null
value.Should().NotBeNull();
value.Should().BeNull();

// Strings
text.Should().Contain("substring");
text.Should().StartWith("prefix");
text.Should().NotBeEmpty();

// Collections
list.Should().HaveCount(3);
list.Should().Contain(item);
list.Should().BeEmpty();

// Exceptions
Action act = () => method();
act.Should().Throw<ArgumentException>();
```

---

## Code Coverage Goals

### Required Coverage

| Component Type          | Minimum Coverage | Target Coverage |
| ----------------------- | ---------------- | --------------- |
| Validators              | 100%             | 100%            |
| Handlers                | 80%              | 90%             |
| Services                | 80%              | 90%             |
| Endpoints (Integration) | 70%              | 80%             |
| Client Components       | 60%              | 70%             |

### Measuring Coverage

```powershell
# Run all tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Generate HTML report
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.opencover.xml -targetdir:coverage-report

# View report
start coverage-report/index.html
```

---

## Running Tests

### Run All Tests

```powershell
dotnet test
```

### Run Specific Project

```powershell
dotnet test API.Tests/API.Tests.csproj
dotnet test Client.Tests/Client.Tests.csproj
dotnet test Shared.Tests/Shared.Tests.csproj
```

### Run Specific Test Class

```powershell
dotnet test --filter "FullyQualifiedName~LoginValidatorTests"
```

### Run Specific Test Method

```powershell
dotnet test --filter "FullyQualifiedName~Should_Have_Error_When_Email_Is_Empty"
```

### Run with Verbose Output

```powershell
dotnet test --logger "console;verbosity=detailed"
```

### Run in Watch Mode

```powershell
dotnet watch test
```

---

## Troubleshooting

### Tests Pass Locally But Fail in CI

**Causes:**

- Database state differs
- Environment variables missing
- Async timing issues

**Solutions:**

- Use InMemory database for tests
- Ensure database is cleaned between tests
- Add explicit waits for async operations

### Mock Not Being Called

**Causes:**

- Mock setup doesn't match actual call
- Handler creates new instance instead of using injected mock

**Solutions:**

```csharp
// Use It.IsAny<T>() for flexible matching
_mockService.Setup(x => x.Method(It.IsAny<string>()))

// Verify exact arguments
_mockService.Verify(
    x => x.Method(It.Is<string>(s => s == expected)),
    Times.Once);
```

### InMemory Database Issues

**Causes:**

- Shared database between tests
- Navigation properties not loading

**Solutions:**

```csharp
// Use unique database name per test
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .Options;

// Explicitly load navigation properties
await context.Entry(entity).Reference(x => x.RelatedEntity).LoadAsync();
```

### bUnit Component Not Finding Elements

**Causes:**

- Component hasn't rendered yet
- Wrong CSS selector
- Element is conditionally rendered

**Solutions:**

```csharp
// Wait for render
cut.WaitForState(() => cut.FindAll("selector").Any());

// Use more specific selectors
var element = cut.Find("button[type='submit']");

// Check if element exists before asserting
var elements = cut.FindAll("selector");
elements.Should().NotBeEmpty();
```

---

## AI Prompt Templates

### Prompt: Create Validator Tests

```
Create comprehensive unit tests for the {Feature}{Action}Validator in API/Features/{Feature}/{Action}/.

Requirements:
- Use FluentValidation.TestHelper
- Test ALL validation rules for each property
- Include tests for:
  - Empty/null values
  - Invalid formats
  - Min/max length violations
  - Pattern mismatches (RegularExpression)
  - Valid values (at least 2 examples per rule)
- Include one test for a completely valid request
- Use [Theory] with [InlineData] for multiple invalid/valid values
- Group tests by property using #region
- Follow naming convention: Should_{ExpectedBehavior}_When_{Condition}
- Add XML summary comment describing what the test class does

File location: API.Tests/Features/{Feature}/{Action}/{Action}ValidatorTests.cs
```

### Prompt: Create Handler Tests

```
Create unit tests for the {Feature}{Action}Handler in API/Features/{Feature}/{Action}/.

Requirements:
- Mock all dependencies (DbContext, Services)
- Test success scenarios (happy path)
- Test all failure scenarios from {Feature}Errors
- Test edge cases (null, empty, boundary values)
- Verify service calls using Moq.Verify
- Use FluentAssertions for all assertions
- Test database operations if applicable
- Follow AAA pattern (Arrange, Act, Assert)
- Add XML summary comment

File location: API.Tests/Features/{Feature}/{Action}/{Action}HandlerTests.cs
```

### Prompt: Create Integration Tests

```
Create integration tests for {Feature} endpoints.

Requirements:
- Use CustomWebApplicationFactory
- Test all HTTP endpoints in {Feature}
- Test success responses (200, 201, 204)
- Test error responses (400, 401, 404, 409, 500)
- Test authentication/authorization if required
- Test complete user flows (multi-step processes)
- Use FluentAssertions
- Follow naming: {Action}_Should_Return_{StatusCode}_When_{Condition}

File location: API.Tests/Integration/{Feature}EndpointsTests.cs
```

### Prompt: Create Component Tests

```
Create bUnit tests for the {ComponentName} component.

Requirements:
- Mock all services (ApiClients, I18nService)
- Test component rendering
- Test user interactions (button clicks, input changes)
- Test API calls are made with correct data
- Test error handling and display
- Test loading states
- Use FluentAssertions
- Follow bUnit best practices

File location: Client.Tests/Components/{Feature}/{ComponentName}Tests.cs
```

### Prompt: Create Postman Collection

```
Create a Postman collection for {Feature} endpoints.

Requirements:
- Include all CRUD operations
- Add test scripts for each request:
  - Status code validation
  - Response time check (< 500ms)
  - Response structure validation
  - Save tokens/IDs to environment variables
- Include both success and failure scenarios
- Use {{baseUrl}} and other environment variables
- Add descriptive names and documentation

File location: Tests/Postman/meeps-api.postman_collection.json
```

---

## Quick Reference Checklist

When creating tests for a new feature, ensure you have:

### API Tests

- [ ] Validator tests (100% coverage)
- [ ] Handler tests (success + all error cases)
- [ ] Integration tests (main endpoints)
- [ ] Mock all external dependencies

### Client Tests

- [ ] Component tests (rendering + interaction)
- [ ] Mock API clients
- [ ] Test error handling

### Shared Tests

- [ ] DTO DataAnnotations tests (if complex validation)

### Postman Tests

- [ ] Add endpoints to collection
- [ ] Add automated test scripts
- [ ] Update environment variables

### General

- [ ] Follow naming conventions
- [ ] Use AAA pattern
- [ ] Add XML comments
- [ ] Run all tests locally
- [ ] Check code coverage

---

**Remember:** Good tests are your best documentation and safety net. Write tests that future you will thank you for! 🎉
