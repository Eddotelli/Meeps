# DELETE ACCOUNT FEATURE - IMPLEMENTATION GUIDE

**Version:** 1.0
**Date:** February 5, 2026
**Feature Branch:** `feature/delete-account`

---

## 📋 OVERVIEW

This guide provides step-by-step instructions for implementing the Delete Account feature in the Meeps application.

### Feature Requirements:
- ✅ User must enter password to confirm deletion
- ✅ User must check confirmation box acknowledging what will be deleted
- ✅ Two-step confirmation (password + final confirmation dialog)
- ✅ Cancel all active events created by user
- ✅ Leave all events user is participating in
- ✅ Send email to participants of cancelled events
- ✅ Messages remain but show "Deleted User" instead of real name
- ✅ Soft delete (not hard delete from database)
- ✅ Revoke all refresh tokens (logout user)

---

## 🎯 WHAT HAPPENS WHEN USER DELETES ACCOUNT

### Will Be Deleted/Modified:
- ✅ User profile set to `IsDeleted = true`
- ✅ DisplayName changed to "Deleted User"
- ✅ Username changed to `deleted_user_{userId}_{timestamp}`
- ✅ Email changed to `deleted_{userId}_{timestamp}@deleted.local`
- ✅ All active events created by user → Status changed to Cancelled
- ✅ User leaves all events they are participating in (EventParticipants removed)
- ✅ All refresh tokens revoked

### Will Be Preserved:
- ✅ Messages in chats (shown as "Deleted User")
- ✅ Event history (soft deleted, not removed)
- ✅ EventParticipants records for history (where user WAS participant)

---

## 📂 IMPLEMENTATION STEPS

### STEP 1: SHARED PROJECT - DTOs & CONSTANTS

#### 1.1 Create Request DTO
**File:** `Shared/Contracts/Users/DeleteAccountRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Shared.Contracts.Users;

public class DeleteAccountRequest
{
    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    
    [Required]
    public bool ConfirmUnderstanding { get; set; }
}
```

#### 1.2 Create Response DTO
**File:** `Shared/Contracts/Users/DeleteAccountResponse.cs`

```csharp
namespace Shared.Contracts.Users;

public class DeleteAccountResponse
{
    public string MessageKey { get; set; } = string.Empty;
    public int CancelledEventsCount { get; set; }
    public int LeftEventsCount { get; set; }
    public int EmailsSentCount { get; set; }
}
```

#### 1.3 Add Error Codes
**File:** `Shared/Common/Constants/ErrorCodes.cs`

**Add these constants:**
```csharp
// Account deletion errors (add to existing ErrorCodes class)
public const string UserIncorrectPassword = "USER.INCORRECT_PASSWORD";
public const string UserDeleteAccountFailed = "USER.DELETE_ACCOUNT_FAILED";
```

#### 1.4 Add Message Keys
**File:** `Shared/Common/Constants/MessageKeys.cs`

**Add these constants:**
```csharp
// Account deletion messages (add to existing MessageKeys class)
public const string AccountDeleted = "accountDeleted";
public const string AccountDeletedWithDetails = "accountDeletedWithDetails";
```

---

### STEP 2: API - DATABASE MIGRATION

#### 2.1 Update User Model
**File:** `API/Models/User.cs`

**Add these properties to the User class:**
```csharp
// Soft Delete fields (add after existing properties)
public bool IsDeleted { get; set; } = false;
public DateTime? DeletedAt { get; set; }
```

#### 2.2 Create and Apply Migration
```bash
cd API
dotnet ef migrations add AddUserSoftDelete
dotnet ef database update
```

---

### STEP 3: API - EMAIL SERVICE

#### 3.1 Update IEmailService Interface
**File:** `API/Infrastructure/Services/IEmailService.cs`

**Add this method to the interface:**
```csharp
Task<Result> SendEventCancelledEmailAsync(string email, string displayName, string eventTitle, string reason);
```

#### 3.2 Update EmailService Implementation
**File:** `API/Infrastructure/Services/EmailService.cs`

**Add this implementation:**
```csharp
public async Task<Result> SendEventCancelledEmailAsync(string email, string displayName, string eventTitle, string reason)
{
    var subject = $"Event Cancelled: {eventTitle}";
    var body = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
            <h2 style='color: #d32f2f;'>Event Cancelled</h2>
            <p>Hello {displayName},</p>
            <p>We regret to inform you that the following event has been cancelled:</p>
            <div style='background-color: #f5f5f5; padding: 15px; border-left: 4px solid #d32f2f; margin: 20px 0;'>
                <h3 style='margin-top: 0;'>{eventTitle}</h3>
                <p><strong>Reason:</strong> {reason}</p>
            </div>
            <p>We apologize for any inconvenience this may cause.</p>
            <p>You can browse other available events in your area on the Meeps app.</p>
            <br>
            <p>Best regards,<br>The Meeps Team</p>
        </div>
    ";
    
    return await SendEmailAsync(email, subject, body);
}
```

#### 3.3 Update FakeEmailService
**File:** `API/Infrastructure/Services/FakeEmailService.cs`

**Add this implementation:**
```csharp
public Task<Result> SendEventCancelledEmailAsync(string email, string displayName, string eventTitle, string reason)
{
    _logger.LogInformation(
        "[FAKE EMAIL] Event Cancelled Email\nTo: {Email}\nName: {DisplayName}\nEvent: {EventTitle}\nReason: {Reason}",
        email, displayName, eventTitle, reason);
    
    return Task.FromResult(Result.Success());
}
```

---

### STEP 4: API - VALIDATOR

#### 4.1 Create Validator
**File:** `API/Features/Users/DeleteAccount/DeleteAccountValidator.cs`

```csharp
using FluentValidation;
using Shared.Contracts.Users;

namespace API.Features.Users.DeleteAccount;

public class DeleteAccountValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required");

        RuleFor(x => x.ConfirmUnderstanding)
            .Equal(true)
            .WithMessage("You must confirm that you understand what will be deleted");
    }
}
```

---

### STEP 5: API - HANDLER (CRITICAL - BUSINESS LOGIC)

#### 5.1 Create Handler
**File:** `API/Features/Users/DeleteAccount/DeleteAccountHandler.cs`

```csharp
using System.Security.Claims;
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using API.Models;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Constants;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Users;
using Shared.Enums;

namespace API.Features.Users.DeleteAccount;

public class DeleteAccountHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<User> _userManager;
    private readonly IValidator<DeleteAccountRequest> _validator;
    private readonly IEmailService _emailService;
    private readonly ILogger<DeleteAccountHandler> _logger;

    public DeleteAccountHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        UserManager<User> userManager,
        IValidator<DeleteAccountRequest> validator,
        IEmailService emailService,
        ILogger<DeleteAccountHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _validator = validator;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<DeleteAccountResponse>> Handle(DeleteAccountRequest request)
    {
        // 1. Get current user ID from JWT claims
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Delete account attempt with invalid user ID claim");
            return Result<DeleteAccountResponse>.Failure(UserErrors.Unauthorized);
        }

        // 2. Find user in database
        var user = await _context.Users
            .Include(u => u.CreatedEvents)
            .Include(u => u.Events)
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            _logger.LogWarning("Delete account attempt for non-existent user: {UserId}", userId);
            return Result<DeleteAccountResponse>.Failure(UserErrors.NotFound);
        }

        if (user.IsDeleted)
        {
            _logger.LogWarning("Delete account attempt for already deleted user: {UserId}", userId);
            return Result<DeleteAccountResponse>.Failure(new Error(
                ErrorCodes.UserDeleteAccountFailed,
                "Account is already deleted"
            ));
        }

        // 3. Verify password using UserManager
        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning("Delete account failed - incorrect password for user: {UserId}", userId);
            return Result<DeleteAccountResponse>.Failure(new Error(
                ErrorCodes.UserIncorrectPassword,
                "Incorrect password"
            ));
        }

        _logger.LogInformation("Starting account deletion for user: {UserId}", userId);

        int cancelledEventsCount = 0;
        int leftEventsCount = 0;
        int emailsSentCount = 0;

        // 4. Cancel all ACTIVE events created by user
        var activeEvents = await _context.Events
            .Include(e => e.EventParticipants)
                .ThenInclude(ep => ep.User)
            .Where(e => e.CreatedByUserId == userId && 
                       e.Status == EventStatus.Active && 
                       !e.IsDeleted)
            .ToListAsync();

        foreach (var evt in activeEvents)
        {
            // Change event status to Cancelled
            evt.Status = EventStatus.Cancelled;
            evt.IsDeleted = true;
            evt.DeletedAt = DateTime.UtcNow;
            evt.DeletedByUserId = userId;

            cancelledEventsCount++;

            // Send email to all participants (except the creator)
            var participants = evt.EventParticipants
                .Where(ep => ep.UserId != userId && !string.IsNullOrEmpty(ep.User.Email))
                .ToList();

            foreach (var participant in participants)
            {
                try
                {
                    var emailResult = await _emailService.SendEventCancelledEmailAsync(
                        participant.User.Email!,
                        participant.User.DisplayName,
                        evt.Title,
                        "The event creator has deleted their account"
                    );

                    if (emailResult.IsSuccess)
                    {
                        emailsSentCount++;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failed to send cancellation email to {Email} for event {EventId}", 
                            participant.User.Email, 
                            evt.Id
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex, 
                        "Exception sending cancellation email to {Email} for event {EventId}", 
                        participant.User.Email, 
                        evt.Id
                    );
                }
            }

            _logger.LogInformation(
                "Cancelled event {EventId} - {EventTitle}. Sent {EmailCount} emails to participants",
                evt.Id,
                evt.Title,
                emailsSentCount
            );
        }

        // 5. Remove user from all events they are participating in (not created by them)
        var participations = await _context.EventParticipants
            .Include(ep => ep.Event)
            .Where(ep => ep.UserId == userId && 
                        ep.Event.CreatedByUserId != userId)
            .ToListAsync();

        foreach (var participation in participations)
        {
            _context.EventParticipants.Remove(participation);
            leftEventsCount++;
            
            _logger.LogInformation(
                "Removed user {UserId} from event {EventId}",
                userId,
                participation.EventId
            );
        }

        // 6. Soft delete user
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;

        // 7. Anonymize user data
        var timestamp = DateTime.UtcNow.Ticks;
        user.DisplayName = "Deleted User";
        user.UserName = $"deleted_user_{userId}_{timestamp}";
        user.Email = $"deleted_{userId}_{timestamp}@deleted.local";
        user.NormalizedUserName = user.UserName.ToUpper();
        user.NormalizedEmail = user.Email.ToUpper();

        // Clear sensitive data
        user.Bio = null;
        user.ProfileImageUrl = null;
        user.DefaultCity = null;
        user.DefaultCityLatitude = null;
        user.DefaultCityLongitude = null;

        // 8. Revoke all refresh tokens (same as logout)
        var activeTokens = user.RefreshTokens
            .Where(rt => !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow)
            .ToList();

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        // 9. Save all changes
        try
        {
            await _context.SaveChangesAsync();
            
            _logger.LogInformation(
                "Successfully deleted account for user {UserId}. " +
                "Cancelled {CancelledEvents} events, left {LeftEvents} events, sent {EmailsSent} emails",
                userId,
                cancelledEventsCount,
                leftEventsCount,
                emailsSentCount
            );

            return Result<DeleteAccountResponse>.Success(new DeleteAccountResponse
            {
                MessageKey = MessageKeys.AccountDeleted,
                CancelledEventsCount = cancelledEventsCount,
                LeftEventsCount = leftEventsCount,
                EmailsSentCount = emailsSentCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete account for user {UserId}", userId);
            return Result<DeleteAccountResponse>.Failure(new Error(
                ErrorCodes.UserDeleteAccountFailed,
                "Failed to delete account. Please try again."
            ));
        }
    }
}
```

---

### STEP 6: API - ENDPOINT

#### 6.1 Create Endpoint
**File:** `API/Features/Users/DeleteAccount/DeleteAccountEndpoint.cs`

```csharp
using API.Common.Extensions;
using Shared.Contracts.Users;

namespace API.Features.Users.DeleteAccount;

public class DeleteAccountEndpoint : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/users/account", Handle)
            .RequireAuthorization()
            .WithTags("Users")
            .WithName("DeleteAccount")
            .Produces<DeleteAccountResponse>(200)
            .Produces(400)
            .Produces(401);
    }

    private static async Task<IResult> Handle(
        DeleteAccountRequest request,
        DeleteAccountHandler handler)
    {
        var result = await handler.Handle(request);
        return result.ToHttpResult();
    }
}
```

---

### STEP 7: API - REGISTRATION

#### 7.1 Register in Program.cs
**File:** `API/Program.cs`

**Add to the validators section (find where other validators are registered):**
```csharp
builder.Services.AddScoped<DeleteAccountValidator>();
```

**Add to the handlers section (find where other handlers are registered):**
```csharp
builder.Services.AddScoped<DeleteAccountHandler>();
```

**No need to add endpoint mapping if you're using automatic endpoint discovery.**

---

### STEP 8: API - UPDATE MESSAGE DISPLAY LOGIC

#### 8.1 Find Message Query Logic
**Look for where messages are queried (likely in `API/Features/Messages/` or `API/Infrastructure/Hubs/ChatHub.cs`)**

**Update the query to show "Deleted User":**
```csharp
// Example - adjust to your actual implementation
var messages = await _context.Messages
    .Include(m => m.User)
    .Where(m => m.EventId == eventId)
    .Select(m => new MessageDto
    {
        Id = m.Id,
        Text = m.Text,
        SentAt = m.SentAt,
        UserId = m.UserId,
        // Show "Deleted User" if user is deleted
        UserDisplayName = m.User.IsDeleted ? "Deleted User" : m.User.DisplayName,
        UserProfileImageUrl = m.User.IsDeleted ? null : m.User.ProfileImageUrl
    })
    .OrderBy(m => m.SentAt)
    .ToListAsync();
```

**Important:** Search for all places where User.DisplayName is used and update to check IsDeleted flag.

---

### STEP 9: CLIENT - API CLIENT

#### 9.1 Update IUsersApiClient Interface
**File:** `Client/Services/ApiClients/IUsersApiClient.cs`

**Add this method to the interface:**
```csharp
Task<Result<DeleteAccountResponse>> DeleteAccountAsync(DeleteAccountRequest request);
```

#### 9.2 Update UsersApiClient Implementation
**File:** `Client/Services/ApiClients/UsersApiClient.cs`

**Add this implementation:**
```csharp
public async Task<Result<DeleteAccountResponse>> DeleteAccountAsync(DeleteAccountRequest request)
{
    return await _apiClient.DeleteAsync<DeleteAccountRequest, DeleteAccountResponse>(
        "/api/users/account",
        request
    );
}
```

---

### STEP 10: CLIENT - LOCALIZATION (CRITICAL - BOTH LANGUAGES!)

#### 10.1 English (en-US)

**File:** `Client/wwwroot/localization/en-US/common.json`
**Add these keys:**
```json
{
  "deleteAccount": "Delete Account",
  "confirmUnderstanding": "I understand the consequences",
  "enterPassword": "Enter your password to confirm",
  "confirmDelete": "Confirm Delete",
  "deleteAccountDanger": "Danger Zone"
}
```

**File:** `Client/wwwroot/localization/en-US/messages.json`
**Add these keys:**
```json
{
  "accountDeleted": "Your account has been successfully deleted. You will be logged out shortly.",
  "accountDeletedWithDetails": "Account deleted successfully. {cancelledEvents} events cancelled, {leftEvents} events left, {emailsSent} notifications sent.",
  "deleteAccountWarning": "THIS ACTION CANNOT BE UNDONE",
  "deleteAccountInfoTitle": "What will happen when you delete your account:",
  "deleteAccountInfoDeleted": "WILL BE DELETED:",
  "deleteAccountInfoDeletedList": "• Your profile and personal information\n• All events you created will be cancelled\n• You will leave all events you're participating in",
  "deleteAccountInfoPreserved": "WILL BE PRESERVED:",
  "deleteAccountInfoPreservedList": "• Your messages in chats (shown as 'Deleted User')",
  "deleteAccountPasswordHelp": "Enter your current password to proceed",
  "deleteAccountFinalConfirm": "Are you absolutely sure you want to delete your account? This action is permanent and cannot be undone."
}
```

**File:** `Client/wwwroot/localization/en-US/errors.json`
**Add these keys:**
```json
{
  "USER.INCORRECT_PASSWORD": "Incorrect password. Please try again.",
  "USER.DELETE_ACCOUNT_FAILED": "Failed to delete account. Please try again later."
}
```

**File:** `Client/wwwroot/localization/en-US/validation.json`
**Add these keys:**
```json
{
  "passwordRequired": "Password is required",
  "confirmUnderstandingRequired": "You must confirm that you understand the consequences"
}
```

**File:** `Client/wwwroot/localization/en-US/helptext.json`
**Add these keys:**
```json
{
  "deleteAccountPassword": "Enter your current password to confirm account deletion",
  "deleteAccountConfirm": "Check this box to confirm you understand what will be deleted"
}
```

#### 10.2 Swedish (sv-SE)

**File:** `Client/wwwroot/localization/sv-SE/common.json`
```json
{
  "deleteAccount": "Ta bort konto",
  "confirmUnderstanding": "Jag förstår konsekvenserna",
  "enterPassword": "Ange ditt lösenord för att bekräfta",
  "confirmDelete": "Bekräfta radering",
  "deleteAccountDanger": "Farlig zon"
}
```

**File:** `Client/wwwroot/localization/sv-SE/messages.json`
```json
{
  "accountDeleted": "Ditt konto har raderats. Du kommer att loggas ut snart.",
  "accountDeletedWithDetails": "Kontot raderat. {cancelledEvents} evenemang avslutades, {leftEvents} evenemang lämnade, {emailsSent} meddelanden skickade.",
  "deleteAccountWarning": "DENNA ÅTGÄRD KAN INTE ÅNGRAS",
  "deleteAccountInfoTitle": "Vad händer när du tar bort ditt konto:",
  "deleteAccountInfoDeleted": "KOMMER ATT RADERAS:",
  "deleteAccountInfoDeletedList": "• Din profil och personliga information\n• Alla evenemang du skapat kommer att avslutas\n• Du kommer att lämna alla evenemang du deltar i",
  "deleteAccountInfoPreserved": "KOMMER ATT BEVARAS:",
  "deleteAccountInfoPreservedList": "• Dina meddelanden i chattar (visas som 'Raderad användare')",
  "deleteAccountPasswordHelp": "Ange ditt nuvarande lösenord för att fortsätta",
  "deleteAccountFinalConfirm": "Är du helt säker på att du vill ta bort ditt konto? Denna åtgärd är permanent och kan inte ångras."
}
```

**File:** `Client/wwwroot/localization/sv-SE/errors.json`
```json
{
  "USER.INCORRECT_PASSWORD": "Felaktigt lösenord. Försök igen.",
  "USER.DELETE_ACCOUNT_FAILED": "Kunde inte radera kontot. Försök igen senare."
}
```

**File:** `Client/wwwroot/localization/sv-SE/validation.json`
```json
{
  "passwordRequired": "Lösenord krävs",
  "confirmUnderstandingRequired": "Du måste bekräfta att du förstår konsekvenserna"
}
```

**File:** `Client/wwwroot/localization/sv-SE/helptext.json`
```json
{
  "deleteAccountPassword": "Ange ditt nuvarande lösenord för att bekräfta radering av konto",
  "deleteAccountConfirm": "Kryssa i denna ruta för att bekräfta att du förstår vad som kommer att raderas"
}
```

---

### STEP 11: CLIENT - DELETE ACCOUNT FORM COMPONENT

#### 11.1 Create Component
**File:** `Client/Components/Settings/DeleteAccountForm.razor`

```razor
@using Microsoft.AspNetCore.Components.Forms
@using Client.Services
@using Client.Services.ApiClients
@using Client.Components.Common
@using Shared.Contracts.Users
@using Shared.Common.Constants
@using Microsoft.AspNetCore.Components.Authorization
@implements IDisposable
@inject II18nService I18n
@inject IUsersApiClient UsersApi
@inject ILogger<DeleteAccountForm> Logger
@inject NavigationManager Navigation
@inject AuthenticationStateProvider AuthStateProvider

<MudGrid>
    <MudItem xs="12">
        <MudCard Elevation="0" Class="modern-section-card" Style="border: 2px solid var(--mud-palette-error);">
            <MudCardContent>
                <div class="modern-section-header">
                    <div class="section-title-wrapper">
                        <MudText Typo="Typo.h6" Class="section-title" Color="Color.Error">
                            <MudIcon Icon="@Icons.Material.Filled.Warning" Class="mr-2" />
                            @I18n.GetCommon("deleteAccountDanger")
                        </MudText>
                        <div class="title-accent" style="background: var(--mud-palette-error);"></div>
                    </div>
                </div>

                @if (!string.IsNullOrEmpty(_errorCode))
                {
                    <MudAlert Severity="Severity.Error" Class="mb-4" Variant="Variant.Filled">
                        @I18n.GetError(_errorCode)
                    </MudAlert>
                }

                <!-- Information about what will happen -->
                <MudAlert Severity="Severity.Info" Class="mb-4" Variant="Variant.Text">
                    <MudText Typo="Typo.body1" Class="font-weight-bold mb-2">
                        @I18n.GetMessage("deleteAccountInfoTitle")
                    </MudText>
                    
                    <MudText Typo="Typo.body2" Class="font-weight-bold mt-3 mb-1">
                        @I18n.GetMessage("deleteAccountInfoDeleted")
                    </MudText>
                    <MudText Typo="Typo.body2" Style="white-space: pre-line;">
                        @I18n.GetMessage("deleteAccountInfoDeletedList")
                    </MudText>
                    
                    <MudText Typo="Typo.body2" Class="font-weight-bold mt-3 mb-1">
                        @I18n.GetMessage("deleteAccountInfoPreserved")
                    </MudText>
                    <MudText Typo="Typo.body2" Style="white-space: pre-line;">
                        @I18n.GetMessage("deleteAccountInfoPreservedList")
                    </MudText>
                </MudAlert>

                <MudAlert Severity="Severity.Warning" Class="mb-4" Variant="Variant.Filled">
                    <MudText Typo="Typo.body1" Class="font-weight-bold">
                        @I18n.GetMessage("deleteAccountWarning")
                    </MudText>
                </MudAlert>

                @if (!_showPasswordForm)
                {
                    <MudButton Color="Color.Error"
                               Variant="Variant.Filled"
                               StartIcon="@Icons.Material.Filled.DeleteForever"
                               OnClick="ShowPasswordForm"
                               Size="Size.Large"
                               Class="rounded-pill">
                        @I18n.GetCommon("deleteAccount")
                    </MudButton>
                }
                else
                {
                    <EditForm Model="_model" OnValidSubmit="HandlePasswordSubmit">
                        <LocalizedDataAnnotationsValidator />

                        <MudText Typo="Typo.body1" Class="mb-3">
                            @I18n.GetMessage("deleteAccountPasswordHelp")
                        </MudText>

                        <MudTextField @bind-Value="_model.Password"
                                      For="@(() => _model.Password)"
                                      Label="@I18n.GetCommon("enterPassword")"
                                      Variant="Variant.Outlined"
                                      InputType="@_passwordInput"
                                      Adornment="Adornment.End"
                                      AdornmentIcon="@_passwordIcon"
                                      OnAdornmentClick="TogglePasswordVisibility"
                                      Immediate="true"
                                      Required="true"
                                      HelperText="@I18n.GetHelpText("deleteAccountPassword")"
                                      Class="mb-3" />

                        <MudCheckBox @bind-Value="_model.ConfirmUnderstanding"
                                     For="@(() => _model.ConfirmUnderstanding)"
                                     Label="@I18n.GetCommon("confirmUnderstanding")"
                                     Color="Color.Error"
                                     Class="mb-4" />

                        <div class="d-flex gap-2">
                            <MudButton Color="Color.Default"
                                       Variant="Variant.Outlined"
                                       OnClick="CancelDelete"
                                       Disabled="_isProcessing"
                                       Size="Size.Large"
                                       Class="rounded-pill">
                                @I18n.GetCommon("cancel")
                            </MudButton>

                            <MudButton ButtonType="ButtonType.Submit"
                                       Color="Color.Error"
                                       Variant="Variant.Filled"
                                       Disabled="_isProcessing || !_model.ConfirmUnderstanding"
                                       Size="Size.Large"
                                       Class="rounded-pill">
                                <Spinner IsLoading="@_isProcessing"
                                         LoadingText="@I18n.GetCommon("processing")"
                                         DefaultText="@I18n.GetCommon("confirmDelete")" />
                            </MudButton>
                        </div>
                    </EditForm>
                }
            </MudCardContent>
        </MudCard>
    </MudItem>
</MudGrid>

<!-- Final Confirmation Dialog -->
<ConfirmationDialog 
    IsVisible="@_showFinalConfirmation"
    IsVisibleChanged="@((v) => _showFinalConfirmation = v)"
    IsProcessing="@_isDeleting"
    Title="@I18n.GetCommon("confirmDelete")"
    Message="@I18n.GetMessage("deleteAccountFinalConfirm")"
    WarningMessage="@I18n.GetMessage("deleteAccountWarning")"
    ConfirmButtonText="@I18n.GetCommon("deleteAccount")"
    ConfirmButtonColor="Color.Error"
    OnConfirm="HandleFinalConfirmation" />

@code {
    private DeleteAccountRequest _model = new();
    private bool _showPasswordForm = false;
    private bool _showFinalConfirmation = false;
    private bool _isProcessing = false;
    private bool _isDeleting = false;
    private string? _errorCode;

    private InputType _passwordInput = InputType.Password;
    private string _passwordIcon = Icons.Material.Filled.VisibilityOff;

    protected override void OnInitialized()
    {
        I18n.OnLanguageChanged += StateHasChanged;
    }

    private void ShowPasswordForm()
    {
        _showPasswordForm = true;
        _errorCode = null;
    }

    private void CancelDelete()
    {
        _showPasswordForm = false;
        _model = new();
        _errorCode = null;
    }

    private void TogglePasswordVisibility()
    {
        if (_passwordInput == InputType.Password)
        {
            _passwordInput = InputType.Text;
            _passwordIcon = Icons.Material.Filled.Visibility;
        }
        else
        {
            _passwordInput = InputType.Password;
            _passwordIcon = Icons.Material.Filled.VisibilityOff;
        }
    }

    private void HandlePasswordSubmit()
    {
        _errorCode = null;
        _showPasswordForm = false;
        _showFinalConfirmation = true;
    }

    private async Task HandleFinalConfirmation()
    {
        _isDeleting = true;
        _errorCode = null;

        Logger.LogInformation("User confirmed account deletion");

        var result = await UsersApi.DeleteAccountAsync(_model);

        if (result.IsSuccess)
        {
            Logger.LogInformation("Account deleted successfully");

            // Give user time to see the confirmation
            await Task.Delay(1000);

            // Logout and redirect to home
            if (AuthStateProvider is CustomAuthenticationStateProvider customAuthProvider)
            {
                await customAuthProvider.LogoutAsync();
            }

            Navigation.NavigateTo("/", forceLoad: true);
        }
        else
        {
            _errorCode = result.Error?.Code ?? ErrorCodes.ServerError;
            Logger.LogWarning("Account deletion failed. Error: {ErrorCode}", _errorCode);
            _showFinalConfirmation = false;
            _showPasswordForm = true;
        }

        _isDeleting = false;
    }

    public void Dispose()
    {
        I18n.OnLanguageChanged -= StateHasChanged;
    }
}
```

---

### STEP 12: CLIENT - ADD TO ACCOUNT SETTINGS

#### 12.1 Update AccountSettings Component
**File:** `Client/Components/Settings/AccountSettings.razor`

**Add at the bottom of the MudGrid (after the email card):**

```razor
    <!-- Add this BEFORE the closing </MudGrid> tag -->
    <MudItem xs="12">
        <MudDivider Class="my-6" />
        <DeleteAccountForm />
    </MudItem>
</MudGrid>
```

**Add to the @using directives at the top:**
```razor
@using Client.Components.Settings
```

---

### STEP 13: TESTING CHECKLIST

#### API Testing:
- [ ] Can call endpoint with valid JWT token
- [ ] Returns 401 if not authenticated
- [ ] Validator validates password requirement
- [ ] Validator validates confirmation checkbox
- [ ] Incorrect password returns correct error code
- [ ] User.IsDeleted set to true
- [ ] User.DisplayName changed to "Deleted User"
- [ ] User.Username anonymized
- [ ] User.Email anonymized
- [ ] Active events status changed to Cancelled
- [ ] User removed from all participating events
- [ ] Emails sent to event participants
- [ ] Refresh tokens revoked
- [ ] Messages remain in database
- [ ] Cannot login after deletion

#### Client Testing:
- [ ] Delete Account section visible in Settings
- [ ] Information alerts show correctly
- [ ] Warning alert displays properly
- [ ] Password form appears when clicking delete
- [ ] Cancel button works
- [ ] Validation works (password required, checkbox required)
- [ ] Password visibility toggle works
- [ ] Final confirmation dialog appears
- [ ] Final confirmation shows all information
- [ ] Error messages display on failure
- [ ] Success leads to logout and redirect
- [ ] Language switching updates all text
- [ ] Loading spinner shows during processing
- [ ] Buttons disabled during processing

#### Integration Testing:
- [ ] After delete: Cannot login with old credentials
- [ ] After delete: Messages show "Deleted User"
- [ ] After delete: Events are cancelled
- [ ] After delete: Participants received emails
- [ ] After delete: User removed from EventParticipants
- [ ] After delete: User's refresh tokens are invalid

---

### STEP 14: FINAL VERIFICATION CHECKLIST

**Before marking feature as complete:**

- [ ] **Shared:** 
  - [ ] DeleteAccountRequest.cs created
  - [ ] DeleteAccountResponse.cs created
  - [ ] ErrorCodes updated
  - [ ] MessageKeys updated

- [ ] **API:**
  - [ ] DeleteAccountValidator.cs created
  - [ ] DeleteAccountHandler.cs created
  - [ ] DeleteAccountEndpoint.cs created
  - [ ] User model has IsDeleted, DeletedAt
  - [ ] Migration created and applied
  - [ ] IEmailService updated
  - [ ] EmailService implementation updated
  - [ ] FakeEmailService implementation updated
  - [ ] Message display logic updated to check IsDeleted
  - [ ] Registrations added to Program.cs
  - [ ] All logging statements added

- [ ] **Client:**
  - [ ] IUsersApiClient interface updated
  - [ ] UsersApiClient implementation updated
  - [ ] DeleteAccountForm.razor created
  - [ ] AccountSettings.razor updated
  - [ ] Localization files updated (BOTH en-US and sv-SE):
    - [ ] common.json
    - [ ] messages.json
    - [ ] errors.json
    - [ ] validation.json
    - [ ] helptext.json

- [ ] **Testing:**
  - [ ] Manual testing completed
  - [ ] All test cases passed
  - [ ] No console errors
  - [ ] No API errors in logs

- [ ] **Documentation:**
  - [ ] Code comments added where necessary
  - [ ] Logging statements are comprehensive
  - [ ] Feature tested in both languages

---

## 🔐 SECURITY CONSIDERATIONS

1. ✅ **Password Verification** - Use `UserManager.CheckPasswordAsync` (NEVER custom comparison)
2. ✅ **Authorization** - Endpoint MUST have `.RequireAuthorization()`
3. ✅ **Soft Delete** - Use soft delete (IsDeleted) instead of hard delete
4. ✅ **Data Anonymization** - Change email, username to prevent collisions
5. ✅ **Token Revocation** - Revoke ALL refresh tokens immediately
6. ✅ **Email Batch** - Send all emails, log failures but don't block deletion
7. ✅ **Audit Trail** - Log user ID, timestamp, counts of affected records
8. ✅ **Transaction Safety** - Consider wrapping in transaction for atomicity

---

## 📊 DATA FLOW

```
User → [Delete Account Button]
    → [Info & Warning Alerts Shown]
    → [Password Form Displayed]
    → [User Enters Password + Checks Confirmation]
    → [Validate Form]
    → [Final ConfirmationDialog]
    → [User Confirms]
    → [API: DELETE /api/users/account]
    → [Verify Password with UserManager]
    → [Cancel Active Events Created by User]
    → [Remove User from All Participating Events]
    → [Send Emails to Event Participants]
    → [Soft Delete User]
    → [Anonymize User Data]
    → [Revoke All Refresh Tokens]
    → [Save Changes to Database]
    → [Return Success to Client]
    → [Client: Logout User]
    → [Client: Redirect to Home]
```

---

## 🐛 TROUBLESHOOTING

### Issue: Password validation fails even with correct password
**Solution:** Ensure you're using `UserManager.CheckPasswordAsync` and not custom comparison

### Issue: Emails not sending
**Solution:** Check email service configuration. Check FakeEmailService is being used in development. Check logs for detailed error messages.

### Issue: User can still login after deletion
**Solution:** Verify that refresh tokens are being revoked. Check that IsDeleted flag is being set. Verify that UserManager respects IsDeleted flag (may need custom UserValidator).

### Issue: Messages still show real user name
**Solution:** Find ALL places where User.DisplayName is queried and add check for IsDeleted flag.

### Issue: Events not being cancelled
**Solution:** Verify EventStatus.Cancelled is correct enum value. Check that events are being queried with correct filters (Status == Active, !IsDeleted).

---

## 📝 NOTES

- This feature uses **soft delete** pattern - data is not removed from database
- Messages are preserved for chat history but show "Deleted User"
- Events are cancelled (soft deleted) not removed
- User is removed from EventParticipants for events they're participating in
- Emails are sent asynchronously and failures are logged but don't block deletion
- All changes are saved in a single transaction for data consistency
- Comprehensive logging is included for debugging and auditing

---

**END OF IMPLEMENTATION GUIDE**

**Version:** 1.0
**Last Updated:** February 5, 2026
