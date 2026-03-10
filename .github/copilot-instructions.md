# Meeps Application - Code Standards & Patterns

## Overview

This document defines the architectural patterns and code standards for the Meeps application. Use this as a reference when implementing new features to ensure consistency across the codebase.

**Architecture:** Vertical Slice Architecture
**Stack:** ASP.NET Core Web API + Blazor WebAssembly + MudBlazor UI
**Patterns:** Result Pattern, Feature Folders, FluentValidation

---

## Table of Contents

1. Project Structure
2. API Feature Implementation
3. Client Feature Implementation
4. Shared Contracts
5. Validation Standards
6. Localization Standards
7. Logging Standards
8. Error Handling & Centralized Messages
9. Implementation Checklist

---

## Project Structure

### Overview

```
Meeps/
├── API/                           # Backend ASP.NET Core Web API
│   ├── Features/                  # Feature-based organization (Vertical Slice)
│   │   └── {FeatureName}/
│   │       └── {ActionName}/
│   │           ├── {ActionName}Endpoint.cs    # HTTP endpoint mapping
│   │           ├── {ActionName}Handler.cs     # Business logic
│   │           └── {ActionName}Validator.cs   # FluentValidation rules
│   ├── Models/                    # EF Core entity models
│   ├── Infrastructure/
│   │   ├── Data/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   └── Configurations/    # EF Core entity configurations
│   │   └── Services/              # Application services (IEmailService, ITokenService, etc.)
│   └── Common/
│       ├── Exceptions/            # GlobalExceptionHandler
│       └── Extensions/            # ResultExtensions, CookieExtensions
│
├── Client/                        # Blazor WebAssembly frontend
│   ├── Pages/                     # Routable pages with @page directive
│   │   └── {Feature}/             # Grouped by feature
│   ├── Components/                # Reusable components
│   │   ├── {Feature}/             # Feature-specific components (forms, cards)
│   │   ├── Common/                # Shared components (Spinner, LocalizedDataAnnotationsValidator)
│   │   └── Layouts/               # Layout components
│   ├── Services/
│   │   ├── ApiClients/            # Type-safe API communication
│   │   ├── I18nService.cs         # Localization service
│   │   └── ValidationErrorMapper.cs
│   └── wwwroot/
│       └── localization/          # Translation JSON files (en-US, sv-SE)
│
└── Shared/                        # Shared between API and Client
    ├── Contracts/                 # Request/Response DTOs with DataAnnotations
    │   └── {Feature}/
    ├── Common/
    │   ├── Results/               # Result<T>, Result, Error classes
    │   └── Errors/                # Static error definitions per feature
    └── Enums/                     # Shared enumerations
```

---

## Feature Implementation Workflow

### Step-by-Step Checklist

När du lägger till en ny feature (t.ex. "CreateEvent"):

#### 1. Shared Project - DTOs

**Location:** `Shared/Contracts/{FeatureName}/`

```csharp
// CreateEventRequest.cs
namespace Shared.Contracts.Events;

public class CreateEventRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9\s]+$")]  // Example: alphanumeric + spaces
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}

// CreateEventResponse.cs
public class CreateEventResponse
{
    public int EventId { get; set; }
    public string Message { get; set; } = string.Empty;
}
```

**✅ VIKTIGT:** Alla DataAnnotations här kommer användas av CLIENT-validering!

#### 2. API - Validator (FluentValidation)

**Location:** `API/Features/{FeatureName}/{ActionName}/{ActionName}Validator.cs`

```csharp
// CreateEventValidator.cs
namespace API.Features.Events.CreateEvent;

public class CreateEventValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(100)
            .Matches(@"^[a-zA-Z0-9\s]+$")
            .WithMessage("Title can only contain letters, numbers and spaces");

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.CategoryId)
            .GreaterThan(0)
            .WithMessage("Please select a valid category");
    }
}
```

**✅ VIKTIGT:** Detta är din SÄKERHETSVALIDERING! Client kan kringgås, API kan inte!

#### 3. API - Handler

**Location:** `API/Features/{FeatureName}/{ActionName}/{ActionName}Handler.cs`

```csharp
// CreateEventHandler.cs
namespace API.Features.Events.CreateEvent;

public class CreateEventHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IValidator<CreateEventRequest> _validator;
    private readonly ILogger<CreateEventHandler> _logger;

    public CreateEventHandler(
        ApplicationDbContext context,
        IValidator<CreateEventRequest> validator,
        ILogger<CreateEventHandler> logger)
    {
        _context = context;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<CreateEventResponse>> Handle(CreateEventRequest request)
    {
        // Validation happens automatically via FluentValidation middleware
        _logger.LogInformation("Creating new event with title: {Title}", request.Title);

        var newEvent = new Event
        {
            Title = request.Title,
            Description = request.Description,
            CategoryId = request.CategoryId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Events.Add(newEvent);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Event created successfully with ID: {EventId}", newEvent.Id);

        return Result<CreateEventResponse>.Success(new CreateEventResponse
        {
            EventId = newEvent.Id,
            MessageKey = MessageKeys.EventCreated  // Use centralized message key
        });
    }
}
```

#### 4. API - Endpoint

**Location:** `API/Features/{FeatureName}/{ActionName}/{ActionName}Endpoint.cs`

```csharp
// CreateEventEndpoint.cs
namespace API.Features.Events.CreateEvent;

public class CreateEventEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/events", Handle)
            .RequireAuthorization()  // If auth is required
            .WithTags("Events");
    }

    private static async Task<IResult> Handle(
        CreateEventRequest request,
        CreateEventHandler handler)
    {
        var result = await handler.Handle(request);
        return result.ToHttpResult();
    }
}
```

#### 5. Client - API Client

**Location:** `Client/Services/ApiClients/{FeatureName}ApiClient.cs`

**✅ VIKTIGT:** Alla API Clients måste ha ett Interface!

```csharp
// IEventsApiClient.cs
namespace Client.Services.ApiClients;

public interface IEventsApiClient
{
    Task<Result<CreateEventResponse>> CreateEvent(CreateEventRequest request);
    Task<Result<GetEventResponse>> GetEvent(int id);
}

// EventsApiClient.cs
namespace Client.Services.ApiClients;

public class EventsApiClient : IEventsApiClient
{
    private readonly ApiClient _apiClient;

    public EventsApiClient(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<Result<CreateEventResponse>> CreateEvent(CreateEventRequest request)
    {
        return await _apiClient.PostAsync<CreateEventRequest, CreateEventResponse>(
            "/api/events",
            request
        );
    }

    public async Task<Result<GetEventResponse>> GetEvent(int id)
    {
        return await _apiClient.GetAsync<GetEventResponse>($"/api/events/{id}");
    }
}
```

**Register in Program.cs:**

```csharp
builder.Services.AddScoped<IEventsApiClient, EventsApiClient>();
```

**Why use Interfaces?**

- ✅ Enables dependency injection
- ✅ Makes testing easier (can mock the interface)
- ✅ Better separation of concerns
- ✅ Follows SOLID principles

#### 6. Client - Localization

**Location:** `Client/wwwroot/localization/{locale}/`

Add to **BOTH** `en-US` and `sv-SE`:

**common.json:**

```json
{
  "title": "Title / Titel",
  "description": "Description / Beskrivning",
  "createEvent": "Create Event / Skapa händelse"
}
```

**validation.json:**

```json
{
  "titleRequired": "Title is required / Titel är obligatorisk",
  "titleLength": "Title must be between 3 and 100 characters / ...",
  "titleInvalidCharacters": "Title can only contain letters, numbers and spaces / ..."
}
```

**helptext.json:**

```json
{
  "title": "Give your event a descriptive title / Ge din händelse en beskrivande titel"
}
```

#### 7. Client - Form Component

**Location:** `Client/Components/{FeatureName}/{ActionName}Form.razor`

See client-component-guidelines.md for full details.

#### 8. Client - Page Component

**Location:** `Client/Pages/{FeatureName}/{ActionName}.razor`

See client-component-guidelines.md for full details.

#### 9. Client - LocalizedDataAnnotationsValidator Update (If needed)

**Location:** `Client/Components/Common/LocalizedDataAnnotationsValidator.razor`

If you added a NEW validation attribute type, update the switch statement:

```csharp
return attribute switch
{
    RequiredAttribute => GetRequiredMessage(propertyName),
    EmailAddressAttribute => I18n.GetValidation("emailAddress"),
    MaxLengthAttribute maxLength => GetMaxLengthMessage(propertyName, maxLength.Length),
    MinLengthAttribute minLength => GetMinLengthMessage(propertyName, minLength.Length),
    StringLengthAttribute stringLength => GetStringLengthMessage(propertyName, stringLength.MinimumLength, stringLength.MaximumLength),
    RangeAttribute range => GetRangeMessage(propertyName, range.Minimum, range.Maximum),
    CompareAttribute compare => GetCompareMessage(propertyName, compare.OtherProperty),
    RegularExpressionAttribute => GetRegularExpressionMessage(propertyName),
    // ADD NEW ATTRIBUTE TYPES HERE!
    _ => originalMessage
};
```

And add corresponding helper method:

```csharp
private string GetRegularExpressionMessage(string propertyName)
{
    var specificKey = $"{ToCamelCase(propertyName)}InvalidCharacters";
    var message = I18n.GetValidation(specificKey);
    return message == specificKey ? I18n.GetValidation("invalidFormat") : message;
}
```

---

## Validation (Critical!)

### ⚠️ VIKTIGT: Validering måste finnas på BÅDA ställen!

#### Why?

- **Client (DataAnnotations)** - För omedelbar användarfeedback, bättre UX
- **API (FluentValidation)** - För säkerhet! Client-validering kan kringgås

### Validation Workflow

```
User Input
    ↓
[Client Validation] ← DataAnnotations på DTO (i Shared/)
    ↓ (passes)
HTTP Request
    ↓
[API Validation] ← FluentValidation Validator (i API/)
    ↓ (passes)
Business Logic
    ↓
Database
```

### Where to Add Validation

| Type                          | Location                                                           | Purpose                          | Example                             |
| ----------------------------- | ------------------------------------------------------------------ | -------------------------------- | ----------------------------------- |
| **DataAnnotations**           | `Shared/Contracts/{Feature}/` on DTOs                              | Client-side validation           | `[Required]`, `[RegularExpression]` |
| **FluentValidation**          | `API/Features/{Feature}/{Action}/{Action}Validator.cs`             | Server-side validation           | `.NotEmpty().Matches(...)`          |
| **LocalizedValidator Update** | `Client/Components/Common/LocalizedDataAnnotationsValidator.razor` | Display localized messages       | Add attribute to switch statement   |
| **Localization Messages**     | `Client/wwwroot/localization/{locale}/validation.json`             | Error messages in both languages | `"fieldInvalidCharacters": "..."`   |

### Validation Attribute Mapping

| DataAnnotation                           | FluentValidation                         | Localization Key Pattern   |
| ---------------------------------------- | ---------------------------------------- | -------------------------- |
| `[Required]`                             | `.NotEmpty()`                            | `{field}Required`          |
| `[EmailAddress]`                         | `.EmailAddress()`                        | `emailAddress`             |
| `[StringLength(max, MinimumLength=min)]` | `.MinimumLength(min).MaximumLength(max)` | `{field}Length`            |
| `[Range(min, max)]`                      | `.InclusiveBetween(min, max)`            | `{field}Range`             |
| `[Compare("Other")]`                     | `.Equal(x => x.Other)`                   | `passwordsDoNotMatch`      |
| `[RegularExpression("regex")]`           | `.Matches("regex")`                      | `{field}InvalidCharacters` |

### Example: Adding Username Validation

**1. Shared DTO:**

```csharp
[Required]
[StringLength(50, MinimumLength = 3)]
[RegularExpression(@"^[a-zA-Z0-9_]+$")]
public string Username { get; set; } = string.Empty;
```

**2. API Validator:**

```csharp
RuleFor(x => x.Username)
    .NotEmpty()
    .MinimumLength(3)
    .MaximumLength(50)
    .Matches(@"^[a-zA-Z0-9_]+$");
```

**3. Localization (both en-US and sv-SE):**

```json
{
  "usernameRequired": "Username is required",
  "usernameLength": "Username must be between 3 and 50 characters",
  "usernameInvalidCharacters": "Username can only contain letters, numbers and underscores"
}
```

**4. LocalizedDataAnnotationsValidator** - Already handles RegularExpression!

---

## Authentication & Authorization

### JWT Token Flow

```
1. Register → Email sent
2. Verify Email → Complete registration with password
3. Login → Access Token (15 min) + Refresh Token (7 days)
4. API calls → Access Token in Authorization header
5. Token expires → Use Refresh Token to get new Access Token
6. Logout → Revoke Refresh Token
```

### User Secrets (Development)

```bash
dotnet user-secrets set "Jwt:Key" "your-long-secret-key-here"
```

### Production Secrets

Set as environment variables:

- `Jwt__Key` (Note: double underscore!)
- `ConnectionStrings__DefaultConnection`
- `Email__Username`
- `Email__Password`

### Protecting Endpoints

```csharp
app.MapPost("/api/events", Handle)
    .RequireAuthorization()  // Requires valid JWT token
    .WithTags("Events");
```

### Getting Current User in Handler

```csharp
public class CreateEventHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public async Task<Result<CreateEventResponse>> Handle(CreateEventRequest request)
    {
        var userId = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        var userIdInt = int.Parse(userId!);

        // Use userId in your logic
    }
}
```

---

## Database & Migrations

### Creating Migrations

After changing entity models:

```bash
cd API
dotnet ef migrations add MigrationName
dotnet ef database update
```

### Common Migration Scenarios

**Add new property to existing entity:**

```bash
dotnet ef migrations add AddBioToUser
dotnet ef database update
```

**Create new entity:**

```bash
dotnet ef migrations add CreateEventEntity
dotnet ef database update
```

### Entity Configuration

**Location:** `API/Infrastructure/Data/Configurations/`

```csharp
// EventConfiguration.cs
public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasOne(e => e.Creator)
            .WithMany(u => u.CreatedEvents)
            .HasForeignKey(e => e.CreatorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**Register in ApplicationDbContext:**

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfiguration(new EventConfiguration());
}
```

---

## API Development

### Vertical Slice Architecture

Each feature is self-contained in its own folder:

```
API/Features/Events/CreateEvent/
├── CreateEventEndpoint.cs    # HTTP endpoint
├── CreateEventHandler.cs     # Business logic
└── CreateEventValidator.cs   # Validation rules
```

### Result Pattern

Always return `Result<T>` or `Result` from handlers:

```csharp
// Success
return Result<CreateEventResponse>.Success(response);

// Failure
return Result<CreateEventResponse>.Failure(EventErrors.NotFound);
```

### Error Definitions

**Location:** `Shared/Common/Errors/{FeatureName}Errors.cs`

```csharp
public static class EventErrors
{
    public static Error NotFound => new("Event.NotFound", "Event not found");
    public static Error AlreadyExists => new("Event.AlreadyExists", "Event already exists");
    public static Error InvalidStatus => new("Event.InvalidStatus", "Invalid event status");
}
```

### Service Registration

**Location:** `API/Program.cs`

```csharp
// Add validators
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add handlers
builder.Services.AddScoped<CreateEventHandler>();

// Add custom services
builder.Services.AddScoped<IEmailService, EmailService>();
```

---

## Client Development

See detailed guidelines in client-component-guidelines.md

### Key Points

- ✅ Always use MudBlazor components
- ✅ All text must be localized (no hardcoded strings!)
- ✅ Separate Page components (logic) from Form components (UI)
- ✅ Use `<Spinner>` for loading states
- ✅ Handle errors gracefully with `<MudAlert>`
- ❌ **NEVER use inline styling** - Always avoid `style=""` attributes. Use Bootstrap classes, MudBlazor component properties, or create CSS classes in `Client/wwwroot/css/app.css` instead

### MudAlert Pattern for Dynamic Localization

**❌ WRONG - Alerts won't update when language changes:**

```csharp
private string errorMessage = string.Empty;
errorMessage = I18n.GetError("AUTH.INVALID_CREDENTIALS"); // Stores static translated text
```

```razor
<MudAlert>@errorMessage</MudAlert>
```

**✅ CORRECT - Alerts update in real-time:**

```csharp
private string? errorCode;  // Store only the key
private string? successMessageKey;  // Store only the key
```

```razor
@if (!string.IsNullOrEmpty(errorCode))
{
    <MudAlert Severity="Severity.Error">@I18n.GetError(errorCode)</MudAlert>
}

@if (!string.IsNullOrEmpty(successMessageKey))
{
    <MudAlert Severity="Severity.Success">@I18n.GetMessage(successMessageKey)</MudAlert>
}
```

**Usage in code:**

```csharp
using Shared.Common.Constants;  // Import centralized constants

if (result.IsFailure)
{
    errorCode = result.Error?.Code ?? ErrorCodes.ServerError;  // Use ErrorCodes constants
    return;
}

successMessageKey = MessageKeys.RegistrationComplete;  // Use MessageKeys constants
```

**Benefits:**

- ✅ Alerts update immediately when user changes language
- ✅ Less code (no computed properties needed)
- ✅ Translation happens only when alert is visible
- ✅ Consistent with other localized components

---

## Logging Standards

### ⚠️ VIKTIGT: Använd alltid ILogger - ALDRIG Console.WriteLine!

**❌ WRONG:**

```csharp
Console.WriteLine("User logged in: " + userId);
Console.WriteLine($"Error occurred: {ex.Message}");
```

**✅ CORRECT:**

```csharp
private readonly ILogger<MyHandler> _logger;

_logger.LogInformation("User logged in with ID: {UserId}", userId);
_logger.LogError(ex, "Error occurred while processing request");
```

### Log Levels

Use appropriate log levels:

| Level              | Usage                      | Example                                                                             |
| ------------------ | -------------------------- | ----------------------------------------------------------------------------------- |
| **LogTrace**       | Detailed diagnostic info   | `_logger.LogTrace("Entering method {MethodName}", nameof(Handle));`                 |
| **LogDebug**       | Development debugging      | `_logger.LogDebug("Query returned {Count} results", results.Count);`                |
| **LogInformation** | General flow tracking      | `_logger.LogInformation("User {UserId} created event {EventId}", userId, eventId);` |
| **LogWarning**     | Abnormal/unexpected events | `_logger.LogWarning("Email send delayed for user {UserId}", userId);`               |
| **LogError**       | Recoverable errors         | `_logger.LogError(ex, "Failed to send email to {Email}", email);`                   |
| **LogCritical**    | System failures            | `_logger.LogCritical(ex, "Database connection failed");`                            |

### Logging Best Practices

✅ **DO:**

- Always inject `ILogger<T>` in constructors
- Use structured logging with named parameters: `{UserId}`, `{EventId}`
- Log exceptions with the exception object: `_logger.LogError(ex, "Message")`
- Log important business events (user actions, state changes)
- Use consistent naming for parameters across the application

❌ **DON'T:**

- Use `Console.WriteLine()` - NEVER!
- Use string interpolation in log messages: ~~`$"User {userId}"`~~
- Use string concatenation: ~~`"User " + userId`~~
- Log sensitive data (passwords, tokens, personal info)
- Over-log (avoid logging in tight loops)

### Example Usage in Handlers

```csharp
public class LoginHandler
{
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(ILogger<LoginHandler> logger)
    {
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> Handle(LoginRequest request)
    {
        _logger.LogInformation("Login attempt for email: {Email}", request.Email);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
        {
            _logger.LogWarning("Login failed - user not found: {Email}", request.Email);
            return Result<LoginResponse>.Failure(new Error(
                ErrorCodes.UserInvalidCredentials,
                "Invalid credentials"
            ));
        }

        _logger.LogInformation("User logged in successfully: {UserId}", user.Id);
        return Result<LoginResponse>.Success(response);
    }
}
```

---

## Localization

### All Translation Files Must Be Updated

When adding ANY user-facing text, update **BOTH** locales:

```
Client/wwwroot/localization/
├── en-US/
│   ├── common.json        # Buttons, labels, field names
│   ├── messages.json      # User messages, notifications
│   ├── helptext.json      # Form field help text
│   ├── errors.json        # Error messages
│   └── validation.json    # Validation messages
└── sv-SE/
    ├── common.json
    ├── messages.json
    ├── helptext.json
    ├── errors.json
    └── validation.json
```

### Usage in Components

```razor
@inject I18nService I18n

<MudTextField Label="@I18n.GetCommon("email")" />
<MudText>@I18n.GetMessage("welcomeMessage")</MudText>
<MudTextField HelperText="@I18n.GetHelpText("password")" />
```

---

## Error Handling & Centralized Messages

### ⚠️ VIKTIGT: Inga hårdkodade felmeddelanden eller success-meddelanden!

**Location:** `Shared/Common/Constants/`

- `ErrorCodes.cs` - Centraliserade error-koder
- `MessageKeys.cs` - Centraliserade success-meddelande nycklar

### ErrorCodes Usage

**❌ WRONG - Hårdkodade error strings:**

```csharp
return Result.Failure(new Error("AUTH.INVALID_CREDENTIALS", "Invalid credentials"));
errorCode = "USER.EMAIL_NOT_VERIFIED";  // Magic string
```

**✅ CORRECT - Använd ErrorCodes konstanter:**

```csharp
using Shared.Common.Constants;

return Result.Failure(new Error(ErrorCodes.UserInvalidCredentials, "Invalid credentials"));
errorCode = ErrorCodes.UserEmailNotVerified;
```

### MessageKeys Usage

**❌ WRONG - Hårdkodade message keys:**

```csharp
successMessageKey = "emailVerificationSent";  // Magic string
return new Response { Message = "Registration complete" };  // Hårdkodat meddelande
```

**✅ CORRECT - Använd MessageKeys konstanter:**

```csharp
using Shared.Common.Constants;

successMessageKey = MessageKeys.EmailVerificationSent;
return new Response { MessageKey = MessageKeys.RegistrationComplete };
```

### Available Error Codes

```csharp
public static class ErrorCodes
{
    // General
    public const string ServerError = "GENERAL.SERVER_ERROR";
    public const string Unauthorized = "GENERAL.UNAUTHORIZED";
    public const string Forbidden = "GENERAL.FORBIDDEN";
    public const string NotFound = "GENERAL.NOT_FOUND";
    public const string ValidationError = "GENERAL.VALIDATION_ERROR";

    // Email
    public const string EmailInvalidToken = "EMAIL.INVALID_TOKEN";
    public const string EmailInvalidLink = "EMAIL.INVALID_LINK";
    public const string EmailSendFailed = "EMAIL.SEND_FAILED";
    public const string EmailAlreadyVerified = "EMAIL.ALREADY_VERIFIED";

    // User/Auth
    public const string UserInvalidCredentials = "USER.INVALID_CREDENTIALS";
    public const string UserEmailNotVerified = "USER.EMAIL_NOT_VERIFIED";
    public const string UserEmailNotFound = "USER.EMAIL_NOT_FOUND";
    public const string UserPasswordResetTokenInvalid = "USER.PASSWORD_RESET_TOKEN_INVALID";
    public const string UserPasswordResetTokenExpired = "USER.PASSWORD_RESET_TOKEN_EXPIRED";

    // Verification
    public const string VerificationFailed = "VERIFICATION.FAILED";
}
```

### Available Message Keys

```csharp
public static class MessageKeys
{
    // Auth
    public const string EmailVerificationSent = "emailVerificationSent";
    public const string PasswordResetEmailSent = "passwordResetEmailSent";
    public const string PasswordResetSuccess = "passwordResetSuccess";
    public const string VerificationComplete = "verificationComplete";
    public const string RegistrationComplete = "registrationComplete";

    // Profile
    public const string ProfileUpdated = "profileUpdated";
    public const string PasswordUpdated = "passwordUpdated";
    public const string EmailUpdated = "emailUpdated";
    public const string PreferencesUpdated = "preferencesUpdated";
}
```

### Adding New Error Codes or Message Keys

When adding a new error or message:

1. **Add to Constants:**
   - `Shared/Common/Constants/ErrorCodes.cs` (för errors)
   - `Shared/Common/Constants/MessageKeys.cs` (för success messages)

2. **Add to Localization Files:**
   - `Client/wwwroot/localization/en-US/errors.json` (för errors)
   - `Client/wwwroot/localization/sv-SE/errors.json`
   - `Client/wwwroot/localization/en-US/messages.json` (för success)
   - `Client/wwwroot/localization/sv-SE/messages.json`

3. **Use in Code:**

   ```csharp
   // API Handler
   return Result.Failure(new Error(ErrorCodes.YourNewError, "Description"));

   // Client Component
   errorCode = ErrorCodes.YourNewError;
   successMessageKey = MessageKeys.YourNewMessage;
   ```

### Complete Example

**API Handler:**

```csharp
using Shared.Common.Constants;

public class RegisterHandler
{
    private readonly ILogger<RegisterHandler> _logger;

    public async Task<Result<RegisterResponse>> Handle(RegisterRequest request)
    {
        _logger.LogInformation("Registration attempt for email: {Email}", request.Email);

        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            _logger.LogWarning("Registration failed - email already exists: {Email}", request.Email);
            return Result<RegisterResponse>.Failure(new Error(
                ErrorCodes.UserEmailAlreadyExists,  // Use constant
                "Email already exists"
            ));
        }

        // ... registration logic ...

        _logger.LogInformation("Registration successful for user: {UserId}", user.Id);

        return Result<RegisterResponse>.Success(new RegisterResponse
        {
            UserId = user.Id,
            MessageKey = MessageKeys.EmailVerificationSent  // Use constant
        });
    }
}
```

**Client Component:**

```csharp
using Shared.Common.Constants;

private string? errorCode;
private string? successMessageKey;

private async Task HandleSubmit()
{
    var result = await AuthApiClient.Register(request);

    if (result.IsFailure)
    {
        errorCode = result.Error?.Code ?? ErrorCodes.ServerError;  // Use constant
        _logger.LogWarning("Registration failed with error: {ErrorCode}", errorCode);
        return;
    }

    successMessageKey = MessageKeys.EmailVerificationSent;  // Use constant
}
```

### Validation Message Naming Convention

For field-specific validation:

- `{fieldName}Required` - e.g., `emailRequired`, `passwordRequired`
- `{fieldName}Length` - e.g., `passwordLength`, `titleLength`
- `{fieldName}InvalidCharacters` - e.g., `usernameInvalidCharacters`
- `{fieldName}Range` - e.g., `ageRange`, `birthYearRange`

For generic validation:

- `required`, `emailAddress`, `invalidFormat`, etc.

---

## Testing Workflow

### Local Development

```bash
# Terminal 1: Run API + Client in watch mode
watch.bat

# Or separately:
# Terminal 1 - API
cd API
dotnet watch run

# Terminal 2 - Client
cd Client
dotnet watch run
```

### Testing Validation

1. Test CLIENT validation:
   - Fill form with invalid data
   - Should see immediate feedback
   - Check that messages are in correct language

2. Test API validation:
   - Use browser dev tools to bypass client validation
   - Send invalid request directly
   - Should receive validation error from API

3. Test localization:
   - Change language in app
   - Verify all messages are translated

### Database Testing

```bash
# Reset database
cd API
dotnet ef database drop
dotnet ef database update
```

---

## Common Issues & Solutions

### Issue: Validation Messages Always in English

**Problem:** Added `RegularExpression` but message doesn't localize.

**Solution:**

1. ✅ Add to DTO in Shared: `[RegularExpression(@"^[a-zA-Z0-9_]+$")]`
2. ✅ Add to API Validator: `.Matches(@"^[a-zA-Z0-9_]+$")`
3. ✅ Add to localization files: `"fieldInvalidCharacters": "..."`
4. ✅ Update `LocalizedDataAnnotationsValidator.razor`:
   - Add `RegularExpressionAttribute` to switch
   - Add `GetRegularExpressionMessage()` method

### Issue: JWT Token Invalid

**Problem:** "401 Unauthorized" on API calls.

**Solution:**

- Check User Secrets: `dotnet user-secrets list`
- Verify JWT key is at least 32 characters
- Check token expiration in browser localStorage
- Try logout/login again

### Issue: Database Migration Fails

**Problem:** `dotnet ef database update` fails.

**Solution:**

```bash
# Check connection string
dotnet user-secrets list

# Drop and recreate
dotnet ef database drop
dotnet ef database update
```

### Issue: API Returns 500 Error

**Problem:** Unhandled exception in API.

**Solution:**

- Check API terminal for stack trace
- Check `GlobalExceptionHandler.cs` is registered
- Verify all required services are injected
- Check database connection

### Issue: Missing Localization Key

**Problem:** Seeing `"keyName"` instead of translated text.

**Solution:**

- Check that key exists in BOTH `en-US` and `sv-SE`
- Check spelling (case-sensitive!)
- Check correct JSON file (common, messages, validation, etc.)
- Refresh browser (Ctrl+Shift+R)

---

## Development Checklist Template

Use this when adding ANY new feature:

### Shared Project

- [ ] Create Request DTO in `Shared/Contracts/{Feature}/`
- [ ] Add DataAnnotations validation
- [ ] Create Response DTO
- [ ] Add Error definitions in `Shared/Common/Errors/` if needed

### API Project

- [ ] Create Validator in `API/Features/{Feature}/{Action}/`
- [ ] Create Handler in `API/Features/{Feature}/{Action}/`
  - [ ] Inject `ILogger<Handler>` in constructor
  - [ ] Add appropriate logging statements
  - [ ] Use `ErrorCodes` constants for errors
  - [ ] Use `MessageKeys` constants for success messages
- [ ] Create Endpoint in `API/Features/{Feature}/{Action}/`
- [ ] Register handler in `Program.cs` if needed
- [ ] Update Entity model if needed
- [ ] Create migration: `dotnet ef migrations add {Name}`
- [ ] Update database: `dotnet ef database update`

### Client Project

- [ ] Create ApiClient Interface in `Client/Services/ApiClients/I{FeatureName}ApiClient.cs`
- [ ] Create ApiClient implementation in `Client/Services/ApiClients/{FeatureName}ApiClient.cs`
- [ ] Register ApiClient with Interface in `Program.cs`: `AddScoped<IFeatureApiClient, FeatureApiClient>()`
- [ ] Create Form component in `Client/Components/{Feature}/`
- [ ] Create Page component in `Client/Pages/{Feature}/`
- [ ] Implement MudAlert pattern (use errorCode/successMessageKey, translate in view)
  - [ ] Use `ErrorCodes` constants for error codes
  - [ ] Use `MessageKeys` constants for success messages
- [ ] Add new ErrorCodes/MessageKeys to `Shared/Common/Constants/` if needed
- [ ] Add ALL localization for BOTH languages:
  - [ ] common.json
  - [ ] messages.json
  - [ ] helptext.json (if applicable)
  - [ ] validation.json (if applicable)
  - [ ] errors.json (if applicable)
- [ ] Update `LocalizedDataAnnotationsValidator.razor` if new attribute type

### Testing

- [ ] Test form validation (client-side)
- [ ] Test API validation (server-side)
- [ ] Test localization (switch languages)
- [ ] Test error handling
- [ ] Test success flow
- [ ] Test with invalid data
- [ ] Test authentication if required

---

## Quick Reference Commands

```bash
# User Secrets
dotnet user-secrets list
dotnet user-secrets set "Key:Name" "Value"
dotnet user-secrets clear

# Database
dotnet ef migrations add MigrationName
dotnet ef database update
dotnet ef database drop

# Run
dotnet watch run           # From API or Client folder
watch.bat                  # From root (runs both)

# Build
dotnet build
dotnet clean

# Git
git status
git add .
git commit -m "message"
git push
git pull
```

---

## Getting Help

1. Check this document first
2. Check client-component-guidelines.md for Client-specific details
3. Check existing features for examples:
   - Authentication: `API/Features/Auth/`
   - Client forms: `Client/Components/Auth/`
4. Check API terminal for errors
5. Check browser console for Client errors
6. Ask your team members!

---

## Best Practices Summary

✅ **DO:**

- Always validate on BOTH client and API
- Always localize text in BOTH languages
- Use Result pattern for API responses
- Use Vertical Slice architecture
- Keep secrets out of Git
- Test validation thoroughly
- **Always use ILogger for logging - NEVER Console.WriteLine**
- **Always use ErrorCodes and MessageKeys constants - NO magic strings**
- Use structured logging with named parameters
- Log important business events and errors

❌ **DON'T:**

- Hardcode text strings
- Commit secrets to Git
- Trust client-side validation alone
- Skip localization
- Forget to update database after migrations
- Skip error handling
- Use inline styling (`style=""` attributes) - use CSS classes or component properties instead
- **Use Console.WriteLine() - ALWAYS use ILogger instead**
- **Hardcode error codes or message keys - ALWAYS use ErrorCodes/MessageKeys constants**
- Log sensitive data (passwords, tokens, etc.)

---

**Last Updated:** January 2026

**Authors:** Meeps Development Team

**Related Documents:**

- client-component-guidelines.md (in Docs/Instructions/)
- LOCALIZED_VALIDATION.md (in Docs/)
