# DeleteEvent Implementation Guide

## Översikt

Detta dokument beskriver implementationen av DeleteEvent funktionalitet med hybrid delete-strategi:

- **Soft Delete**: För events som har passerat sitt datum (behåll historik)
- **Hard Delete**: För events som inte hänt än (GDPR-compliant)

## Projektstruktur

Följer Vertical Slice Architecture enligt projektstandard.

---

## DEL 1: Databasändringar (Soft Delete Support)

### 1.1 Uppdatera Event Model

**Fil:** `API/Models/Event.cs`

**Lägg till följande properties i Event-klassen:**

```csharp
// Soft Delete fields
public bool IsDeleted { get; set; } = false;
public DateTime? DeletedAt { get; set; }
public int? DeletedByUserId { get; set; }
public User? DeletedByUser { get; set; }
```

**Full property lista (efter ändring):**

- IsDeleted: Flag för soft deleted events (default false)
- DeletedAt: Timestamp för när eventet raderades (för audit trail)
- DeletedByUserId: FK till User som raderade eventet (nullable)
- DeletedByUser: Navigation property till användare som raderade

### 1.2 Uppdatera EventConfiguration

**Fil:** `API/Infrastructure/Data/Configurations/EventConfiguration.cs`

**I Configure-metoden, lägg till efter befintliga konfigurationer:**

```csharp
// Soft delete configuration
builder.Property(e => e.IsDeleted)
    .HasDefaultValue(false);

builder.HasOne(e => e.DeletedByUser)
    .WithMany()
    .HasForeignKey(e => e.DeletedByUserId)
    .OnDelete(DeleteBehavior.Restrict);

// Add indexes for performance
builder.HasIndex(e => e.IsDeleted);
builder.HasIndex(e => e.DeletedAt);

// Query filter - hide soft deleted events by default
builder.HasQueryFilter(e => !e.IsDeleted);
```

**Viktigt:**

- `HasQueryFilter(e => !e.IsDeleted)` döljer automatiskt soft-deleted events i alla queries
- Använd `.IgnoreQueryFilters()` när du behöver se soft-deleted events

### 1.3 Skapa och kör Migration

**Kommando:**

```bash
cd API
dotnet ef migrations add AddSoftDeleteToEvents
dotnet ef database update
```

**Migration skapar:**

- IsDeleted kolumn (bit, default 0)
- DeletedAt kolumn (datetime2, nullable)
- DeletedByUserId kolumn (int, nullable)
- Index på IsDeleted
- Index på DeletedAt
- Foreign Key: DeletedByUserId → Users.Id (RESTRICT)

---

## DEL 2: SHARED - DTOs och Constants

### 2.1 Skapa DeleteEventRequest

**Fil:** `Shared/Contracts/Events/DeleteEventRequest.cs`

```csharp
namespace Shared.Contracts.Events;

public class DeleteEventRequest
{
    public int EventId { get; set; }
}
```

### 2.2 Skapa DeleteEventResponse

**Fil:** `Shared/Contracts/Events/DeleteEventResponse.cs`

```csharp
namespace Shared.Contracts.Events;

public class DeleteEventResponse
{
    public string MessageKey { get; set; } = string.Empty;
}
```

### 2.3 Uppdatera EventErrors

**Fil:** `Shared/Common/Errors/EventErrors.cs`

**Lägg till följande error definitions:**

```csharp
public static readonly Error AlreadyDeleted = new(
    "EVENT.ALREADY_DELETED",
    "Event has already been deleted",
    400);

public static readonly Error CannotDeleteWithParticipants = new(
    "EVENT.CANNOT_DELETE_WITH_PARTICIPANTS",
    "Cannot delete event with active participants",
    400);
```

### 2.4 Uppdatera MessageKeys

**Fil:** `Shared/Common/Constants/MessageKeys.cs`

**Lägg till:**

```csharp
public const string EventDeleted = "eventDeleted";
```

### 2.5 Uppdatera ErrorCodes

**Fil:** `Shared/Common/Constants/ErrorCodes.cs`

**Lägg till:**

```csharp
public const string EventCannotDelete = "EVENT.CANNOT_DELETE";
public const string EventAlreadyDeleted = "EVENT.ALREADY_DELETED";
```

---

## DEL 3: BACKEND - DeleteEvent Feature

### 3.1 Skapa DeleteEventValidator

**Fil:** `API/Features/Events/DeleteEvent/DeleteEventValidator.cs`

```csharp
using FluentValidation;
using Shared.Contracts.Events;

namespace API.Features.Events.DeleteEvent;

public class DeleteEventValidator : AbstractValidator<DeleteEventRequest>
{
    public DeleteEventValidator()
    {
        RuleFor(x => x.EventId)
            .GreaterThan(0)
            .WithMessage("EventId must be greater than 0");
    }
}
```

### 3.2 Skapa DeleteEventHandler

**Fil:** `API/Features/Events/DeleteEvent/DeleteEventHandler.cs`

```csharp
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Events;
using Shared.Common.Constants;
using System.Security.Claims;

namespace API.Features.Events.DeleteEvent;

public class DeleteEventHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IImageStorageService _imageStorageService;
    private readonly ILogger<DeleteEventHandler> _logger;

    public DeleteEventHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IImageStorageService imageStorageService,
        ILogger<DeleteEventHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _imageStorageService = imageStorageService;
        _logger = logger;
    }

    public async Task<Result<DeleteEventResponse>> HandleAsync(int eventId)
    {
        _logger.LogInformation("Delete request for event {EventId}", eventId);

        // Get current user ID
        var userId = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Unauthorized attempt to delete event {EventId}", eventId);
            return Result.Failure<DeleteEventResponse>(AuthErrors.InvalidCredentials);
        }

        var userIdInt = int.Parse(userId);

        // Find event - include related data and ignore soft delete filter
        var eventEntity = await _context.Events
            .IgnoreQueryFilters() // To check if already soft deleted
            .Include(e => e.EventParticipants)
            .Include(e => e.Messages)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (eventEntity == null)
        {
            _logger.LogWarning("Event {EventId} not found", eventId);
            return Result.Failure<DeleteEventResponse>(EventErrors.NotFound);
        }

        // Check if already soft deleted
        if (eventEntity.IsDeleted)
        {
            _logger.LogWarning("Event {EventId} is already deleted", eventId);
            return Result.Failure<DeleteEventResponse>(EventErrors.AlreadyDeleted);
        }

        // Check if user is the creator
        if (eventEntity.CreatedByUserId != userIdInt)
        {
            _logger.LogWarning(
                "User {UserId} attempted to delete event {EventId} but is not the creator",
                userIdInt, eventId);
            return Result.Failure<DeleteEventResponse>(EventErrors.Unauthorized);
        }

        var currentTime = DateTime.UtcNow;
        var eventHasPassed = eventEntity.DateTime < currentTime;

        if (eventHasPassed)
        {
            // SOFT DELETE - Event has already happened, keep for history
            _logger.LogInformation(
                "Soft deleting event {EventId} (event date: {EventDate} has passed)",
                eventId, eventEntity.DateTime);

            eventEntity.IsDeleted = true;
            eventEntity.DeletedAt = currentTime;
            eventEntity.DeletedByUserId = userIdInt;
            eventEntity.Status = Shared.Enums.EventStatus.Cancelled;

            // EventParticipants & Messages are kept (cascade delete does not trigger)
        }
        else
        {
            // HARD DELETE - Event hasn't happened yet, no purpose to keep data
            _logger.LogInformation(
                "Hard deleting event {EventId} (event date: {EventDate} is in future)",
                eventId, eventEntity.DateTime);

            // Remove event from database
            // CASCADE will automatically delete:
            // - EventParticipants
            // - Messages
            _context.Events.Remove(eventEntity);

            // Delete image from disk if exists
            if (!string.IsNullOrEmpty(eventEntity.ImageUrl))
            {
                _logger.LogInformation("Deleting event image: {ImageUrl}", eventEntity.ImageUrl);
                await _imageStorageService.DeleteImageAsync(eventEntity.ImageUrl);
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Event {EventId} deleted successfully (soft: {IsSoft})",
            eventId, eventHasPassed);

        return Result<DeleteEventResponse>.Success(new DeleteEventResponse
        {
            MessageKey = MessageKeys.EventDeleted
        });
    }
}
```

**Logik:**

1. Hämta userId från claims
2. Hitta event (med `.IgnoreQueryFilters()` för att kunna se redan soft-deleted)
3. Verifiera att event finns och inte redan är raderad
4. Verifiera att användaren är creator
5. Kolla om event har passerat (`DateTime < currentTime`)
   - **JA (passerat)**: Soft delete (sätt flags, behåll data)
   - **NEJ (framtida)**: Hard delete (ta bort från DB, radera bild)
6. SaveChanges
7. Return success

### 3.3 Skapa DeleteEventEndpoint

**Fil:** `API/Features/Events/DeleteEvent/DeleteEventEndpoint.cs`

```csharp
using API.Common.Extensions;
using Shared.Contracts.Events;

namespace API.Features.Events.DeleteEvent;

public static class DeleteEventEndpoint
{
    public static IEndpointRouteBuilder MapDeleteEvent(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/events/{id:int}", async (
            int id,
            DeleteEventHandler handler) =>
        {
            var result = await handler.HandleAsync(id);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithTags("Events")
        .WithName("DeleteEvent")
        .Produces<DeleteEventResponse>(200)
        .Produces(401)
        .Produces(403)
        .Produces(404);

        return app;
    }
}
```

### 3.4 Registrera i Program.cs

**Fil:** `API/Program.cs`

**Lägg till handler i DI container (hitta där andra handlers registreras):**

```csharp
builder.Services.AddScoped<DeleteEventHandler>();
```

**Registrera endpoint (hitta där andra event endpoints mappas):**

```csharp
app.MapDeleteEvent();
```

---

## DEL 4: FRONTEND - API Client

### 4.1 Uppdatera IEventsApiClient Interface

**Fil:** `Client/Services/ApiClients/IEventsApiClient.cs`

**Lägg till method i interface:**

```csharp
/// <summary>
/// Deletes an event.
/// </summary>
/// <param name="eventId">Event ID to delete</param>
/// <returns>Result with deletion confirmation</returns>
Task<Result<DeleteEventResponse>> DeleteEventAsync(int eventId);
```

### 4.2 Implementera i EventsApiClient

**Fil:** `Client/Services/ApiClients/EventsApiClient.cs`

**Lägg till method i klassen:**

```csharp
public Task<Result<DeleteEventResponse>> DeleteEventAsync(int eventId)
    => _apiClient.DeleteAsync<DeleteEventResponse>($"/api/events/{eventId}");
```

---

## DEL 5: FRONTEND - UI Implementation

### 5.1 Uppdatera EventDetails.razor

**Fil:** `Client/Pages/Events/EventDetails.razor`

#### 5.1.1 Lägg till state variables i @code:

```csharp
private bool isDeleting = false;
private bool showDeleteDialog = false;
```

#### 5.1.2 Uppdatera Delete-knappen (hitta runt rad 282):

**Hitta:**

```razor
<MudButton Variant="Variant.Outlined" Color="Color.Error" StartIcon="@Icons.Material.Filled.Delete"
    Size="Size.Large">
    @I18n.GetCommon("delete")
</MudButton>
```

**Ersätt med:**

```razor
<MudButton Variant="Variant.Outlined"
           Color="Color.Error"
           StartIcon="@Icons.Material.Filled.Delete"
           Size="Size.Large"
           OnClick="ShowDeleteConfirmation"
           Disabled="isDeleting">
    @I18n.GetCommon("delete")
</MudButton>
```

#### 5.1.3 Lägg till confirmation dialog (efter befintlig MudCard, innan sista </MudItem>):

```razor
@* Delete Confirmation Dialog *@
<MudDialog @bind-IsVisible="showDeleteDialog" Options="new DialogOptions { CloseButton = true }">
    <TitleContent>
        <MudText Typo="Typo.h6">
            @I18n.GetCommon("confirmAction")
        </MudText>
    </TitleContent>
    <DialogContent>
        <MudText>@I18n.GetMessage("confirmDeleteEvent")</MudText>
        <MudText Typo="Typo.body2" Color="Color.Error" Class="mt-2">
            @I18n.GetMessage("actionCannotBeUndone")
        </MudText>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="() => showDeleteDialog = false" Disabled="isDeleting">
            @I18n.GetCommon("cancel")
        </MudButton>
        <MudButton Color="Color.Error"
                   Variant="Variant.Filled"
                   OnClick="HandleDeleteEvent"
                   Disabled="isDeleting">
            @if (isDeleting)
            {
                <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="mr-2" />
            }
            @I18n.GetCommon("delete")
        </MudButton>
    </DialogActions>
</MudDialog>
```

#### 5.1.4 Lägg till methods i @code block:

```csharp
private void ShowDeleteConfirmation()
{
    showDeleteDialog = true;
}

private async Task HandleDeleteEvent()
{
    isDeleting = true;
    errorCode = null;

    _logger.LogInformation("Attempting to delete event {EventId}", id);

    var result = await EventsApiClient.DeleteEventAsync(id);

    if (result.IsFailure)
    {
        errorCode = result.Error?.Code ?? ErrorCodes.ServerError;
        _logger.LogWarning("Failed to delete event: {ErrorCode}", errorCode);
        isDeleting = false;
        showDeleteDialog = false;
        return;
    }

    _logger.LogInformation("Event deleted successfully");
    successMessageKey = MessageKeys.EventDeleted;

    // Navigate after short delay to show success message
    await Task.Delay(1500);
    NavigationManager.NavigateTo("/events/my-events");
}
```

**Viktigt:**

- Använd `ErrorCodes` och `MessageKeys` constants
- Använd `errorCode` och `successMessageKey` state (inte direkt översättning)
- Detta gör att alerts uppdateras när språk byts

---

## DEL 6: FRONTEND - Localization

### 6.1 Engelska Översättningar

**Fil:** `Client/wwwroot/localization/en-US/common.json`

**Lägg till (om de inte redan finns):**

```json
{
  "confirmAction": "Confirm Action",
  "cancel": "Cancel",
  "delete": "Delete"
}
```

**Fil:** `Client/wwwroot/localization/en-US/messages.json`

**Lägg till:**

```json
{
  "eventDeleted": "Event deleted successfully",
  "confirmDeleteEvent": "Are you sure you want to delete this event?",
  "actionCannotBeUndone": "This action cannot be undone."
}
```

**Fil:** `Client/wwwroot/localization/en-US/errors.json`

**Lägg till:**

```json
{
  "EVENT.CANNOT_DELETE": "Cannot delete this event",
  "EVENT.ALREADY_DELETED": "Event has already been deleted"
}
```

### 6.2 Svenska Översättningar

**Fil:** `Client/wwwroot/localization/sv-SE/common.json`

**Lägg till (om de inte redan finns):**

```json
{
  "confirmAction": "Bekräfta åtgärd",
  "cancel": "Avbryt",
  "delete": "Radera"
}
```

**Fil:** `Client/wwwroot/localization/sv-SE/messages.json`

**Lägg till:**

```json
{
  "eventDeleted": "Händelse raderad",
  "confirmDeleteEvent": "Är du säker på att du vill radera denna händelse?",
  "actionCannotBeUndone": "Denna åtgärd kan inte ångras."
}
```

**Fil:** `Client/wwwroot/localization/sv-SE/errors.json`

**Lägg till:**

```json
{
  "EVENT.CANNOT_DELETE": "Kan inte radera denna händelse",
  "EVENT.ALREADY_DELETED": "Händelsen har redan raderats"
}
```

---

## DEL 7: TESTS (Optional men rekommenderat)

### 7.1 Integration Tests

**Fil:** `API.Tests/Integration/EventEndpointsTests.cs`

**Lägg till test methods:**

```csharp
[Fact]
public async Task DeleteEvent_AsCreator_FutureEvent_HardDeletes_ReturnsSuccess()
{
    // Arrange: Create event in future
    // Act: DELETE /api/events/{id}
    // Assert: 200 OK, event removed from DB
}

[Fact]
public async Task DeleteEvent_AsCreator_PastEvent_SoftDeletes_ReturnsSuccess()
{
    // Arrange: Create event in past
    // Act: DELETE /api/events/{id}
    // Assert: 200 OK, event.IsDeleted = true, data remains
}

[Fact]
public async Task DeleteEvent_AsNonCreator_ReturnsForbidden()
{
    // Arrange: Create event as User A
    // Act: DELETE as User B
    // Assert: 403 Forbidden
}

[Fact]
public async Task DeleteEvent_NonExistentEvent_ReturnsNotFound()
{
    // Act: DELETE /api/events/99999
    // Assert: 404 Not Found
}

[Fact]
public async Task DeleteEvent_WithoutAuth_ReturnsUnauthorized()
{
    // Act: DELETE without token
    // Assert: 401 Unauthorized
}

[Fact]
public async Task DeleteEvent_AlreadyDeleted_ReturnsError()
{
    // Arrange: Soft delete event
    // Act: Try to delete again
    // Assert: 400 Bad Request, EVENT.ALREADY_DELETED
}
```

### 7.2 Postman Tests

**Location:** `Tests/Postman/`

**Skapa tests för:**

1. DELETE `/api/events/{id}` - Success (future event = hard delete)
2. DELETE `/api/events/{id}` - Success (past event = soft delete)
3. DELETE `/api/events/{id}` - Unauthorized (no token)
4. DELETE `/api/events/{id}` - Forbidden (not creator)
5. DELETE `/api/events/{id}` - Not found
6. DELETE `/api/events/{id}` - Already deleted

---

## Implementationsordning

### Rekommenderad ordning:

1. **Databas först (DEL 1)**
   - Uppdatera Event model
   - Uppdatera EventConfiguration
   - Skapa och kör migration
   - Testa att databasen är uppdaterad

2. **Shared layer (DEL 2)**
   - Skapa DTOs
   - Uppdatera error definitions
   - Uppdatera constants

3. **Backend (DEL 3)**
   - Skapa Validator
   - Skapa Handler
   - Skapa Endpoint
   - Registrera i Program.cs

4. **Frontend - API (DEL 4)**
   - Uppdatera interface
   - Implementera i ApiClient

5. **Frontend - UI (DEL 5)**
   - Uppdatera EventDetails.razor
   - Lägg till state och methods

6. **Localization (DEL 6)**
   - Lägg till alla översättningar (både en-US och sv-SE)

7. **Tests (DEL 7)**
   - Integration tests
   - Postman tests

---

## Validering & Testning

### Efter implementation, verifiera:

#### Backend:

```bash
# Testa migration är applied
cd API
dotnet ef migrations list

# Kolla att senaste migration är "AddSoftDeleteToEvents"
```

#### API Endpoints:

1. Skapa ett test-event i framtiden
2. DELETE via Postman/Swagger → Verifiera hard delete (event borta från DB)
3. Skapa ett test-event i det förflutna
4. DELETE via Postman/Swagger → Verifiera soft delete (IsDeleted = true, data kvar)

#### Frontend:

1. Gå till event details som creator
2. Klicka på Delete-knappen
3. Verifiera att confirmation dialog visas
4. Klicka på Delete i dialog
5. Verifiera att success-meddelande visas
6. Verifiera att redirect till "/events/my-events" sker
7. Byt språk → Verifiera att alla texter översätts korrekt

#### Query Filter:

```csharp
// Testa att soft-deleted events döljs automatiskt
var events = await _context.Events.ToListAsync(); // Ska INTE inkludera IsDeleted = true

// Testa att man kan se soft-deleted med IgnoreQueryFilters
var allEvents = await _context.Events.IgnoreQueryFilters().ToListAsync(); // Ska inkludera alla
```

---

## Viktiga Noteringar

### Query Filter Impact:

- Med `HasQueryFilter(e => !e.IsDeleted)` kommer ALLA EF Core queries automatiskt filtrera bort soft-deleted events
- Befintliga queries behöver INTE ändras
- För att se soft-deleted events (t.ex. admin panel): använd `.IgnoreQueryFilters()`

### GDPR Compliance:

- Soft delete för passerade events = OK för historik
- Hard delete för framtida events = OK för GDPR
- **Rekommendation:** Lägg till background job som hard deletar gamla soft-deleted events (2+ år)

### Cascade Behavior:

- **Hard Delete**: EventParticipants & Messages raderas automatiskt (CASCADE)
- **Soft Delete**: EventParticipants & Messages behålls (cascade triggar ej)

### Bildhantering:

- Vid hard delete: Radera bild från disk via `IImageStorageService.DeleteImageAsync()`
- Vid soft delete: Behåll bild (event kan fortfarande visas i historik)

### Logging:

- Använd ALLTID `ILogger` (ALDRIG `Console.WriteLine`)
- Logga viktiga events: delete attempts, success, failures
- Använd structured logging med named parameters

### Error Handling:

- Använd `ErrorCodes` constants
- Använd `MessageKeys` constants
- Implementera MudAlert pattern (lagra errorCode/successMessageKey, översätt i view)

---

## Felsökning

### Migration-problem:

```bash
# Om migration failar
cd API
dotnet ef database drop
dotnet ef database update
```

### Query Filter-problem:

- Om befintliga queries inte fungerar, kolla om de förväntar sig soft-deleted events
- Lägg till `.IgnoreQueryFilters()` där nödvändigt

### Cascade Delete-problem:

- Om hard delete failar med FK constraint: Kontrollera att CASCADE är korrekt konfigurerat
- Använd `.Include()` för att ladda relaterade entities

---

## Nästa Steg (Framtida Förbättringar)

1. **Admin Panel**: Endpoint för att lista och återställa soft-deleted events
2. **Background Job**: Scheduled task för att hard delete gamla soft-deleted events (2+ år)
3. **Anonymization**: Istället för att behålla all data, anonymisera user-info i gamla events
4. **Metrics**: Logga statistik över hard vs soft deletes
5. **Data Export**: GDPR rätt-till-data-portabilitet för deleted events

---

## Sammanfattning

Efter implementationen kommer systemet att:
✅ Soft delete events som har passerat (behåll historik)
✅ Hard delete events som inte hänt än (GDPR-compliant)
✅ Automatiskt dölja soft-deleted events i alla queries
✅ Ha fullständig audit trail (vem, när, varför)
✅ Ha en användarvänlig confirmation dialog
✅ Vara fullständigt lokaliserad (en-US & sv-SE)
✅ Följa projektets Vertical Slice Architecture

**Total utvecklingstid:** Ca 2-3 timmar för komplett implementation + testning

**Frågor eller problem?** Se projektets huvuddokumentation i `.github/copilot-instructions.md`
