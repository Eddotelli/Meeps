using System.Text;
using System.Threading.RateLimiting;
using API.Common.Exceptions;
using API.Features.Auth.CheckAuth;
using API.Features.Auth.CompleteRegistration;
using API.Features.Auth.ForgotPassword;
using API.Features.Auth.Login;
using API.Features.Auth.Logout;
using API.Features.Auth.Register;
using API.Features.Auth.RefreshToken;
using API.Features.Auth.ResetPassword;
using API.Features.Auth.ValidateResetToken;
using API.Features.Auth.VerifyEmail;
using API.Features.Events.CreateEvent;
using API.Features.Events.BlockParticipant;
using API.Features.Events.UnblockParticipant;
using API.Features.Events.GetEventDetails;
using API.Features.Events.GetMyEvents;
using API.Features.Events.GetMyParticipatingEvents;
using API.Features.Events.GetMyArchivedEvents;
using API.Features.Events.GetEligibleEvents;
using API.Features.Events.UpdateEvent;
using API.Features.Events.GetEventEditConstraints;
using API.Features.Events.JoinEvent;
using API.Features.Events.LeaveEvent;
using API.Features.Events.DeleteEvent;
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using API.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
#if DEBUG
using API.Features.Test;
#endif

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Meeps API",
        Version = "v1",
        Description = "API för Meeps event-plattform"
    });

    // Add JWT authentication to Swagger
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Database - Use InMemory for testing environment
if (builder.Environment.EnvironmentName == "Testing")
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("TestDatabase"));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions => sqlOptions.EnableRetryOnFailure()));
}

// Identity
builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // User settings
    options.User.RequireUniqueEmail = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? (builder.Environment.EnvironmentName == "Testing"
        ? "ThisIsATestKeyForIntegrationTestingPurposesOnly123456789"
        : throw new InvalidOperationException(
            "JWT Key MUST be configured! Use User Secrets (dev) or Environment Variables (prod)."));

// Validate JWT key length (minimum 256 bits = 32 characters)
if (jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        $"JWT Key must be at least 32 characters long for security. Current length: {jwtKey.Length}");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TestIssuer";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TestAudience";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = true; // Always require HTTPS for security
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromSeconds(5) // Allow 5 seconds tolerance for clock skew between servers
    };

    // Read JWT token from cookie instead of Authorization header
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Try to get token from cookie first
            var accessToken = context.Request.Cookies["AccessToken"];

            // For SignalR: Check query string for access token
            // SignalR can't send custom headers, so token is sent via query string
            if (string.IsNullOrEmpty(accessToken))
            {
                var path = context.HttpContext.Request.Path;

                // Only allow token from query string for SignalR hub
                if (path.StartsWithSegments("/hubs"))
                {
                    accessToken = context.Request.Query["access_token"];
                }
            }

            if (!string.IsNullOrEmpty(accessToken))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

// Authorization Policies 
builder.Services.AddAuthorization(options =>
{
    // Require User role (or Admin which includes User permissions)
    options.AddPolicy("RequireUser", policy =>
        policy.RequireRole("User", "Admin"));

    // Require Admin role only
    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireRole("Admin"));
});

// Rate Limiting - Protect against brute force and DDoS attacks
// Disabled in Testing environment to prevent test failures
if (builder.Environment.EnvironmentName != "Testing")
{
    builder.Services.AddRateLimiter(options =>
{
    // Auth endpoints rate limit: 5 requests per minute per IP
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Token refresh: 10 requests per minute (more lenient for legitimate use)
    options.AddFixedWindowLimiter("token-refresh", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // General API rate limit: 100 requests per minute
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc6585#section-4",
            title = "Too Many Requests",
            status = 429,
            detail = "Rate limit exceeded. Please try again later."
        }, cancellationToken);
    };
});
}

// Exception Handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add HttpContextAccessor for IP and UserAgent logging
builder.Services.AddHttpContextAccessor();

// Bind Email settings
builder.Services.Configure<API.Infrastructure.Configuration.EmailSettings>(
    builder.Configuration.GetSection("Email"));

// Bind Image settings
builder.Services.Configure<API.Infrastructure.Configuration.ImageSettings>(
    builder.Configuration.GetSection("ImageSettings"));

// Bind Google AI settings (Gemini API or Vertex AI)
builder.Services.Configure<API.Infrastructure.Configuration.GoogleAISettings>(
    builder.Configuration.GetSection("GoogleAI"));

// Infrastructure Services
// Use FakeEmailService in Testing environment to avoid sending real emails during automated tests
if (builder.Environment.EnvironmentName == "Testing")
{
    builder.Services.AddSingleton<IEmailService, FakeEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, EmailService>();
}
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEventPermissionsService, EventPermissionsService>();

// Image Services - Gemini API
builder.Services.AddHttpClient();
builder.Services.AddScoped<IImageGenerationService, GeminiImageService>();
builder.Services.AddScoped<IImageStorageService, ImageStorageService>();
builder.Services.AddScoped<IImageService, ImageService>();

// Moderation Service - Gemini API
builder.Services.AddScoped<IGeminiModerationService, GeminiModerationService>();

// Background Task Queue for async message moderation
builder.Services.AddSingleton<API.Common.BackgroundTasks.IBackgroundTaskQueue, API.Common.BackgroundTasks.BackgroundTaskQueue>();
builder.Services.AddHostedService<API.Infrastructure.Services.MessageModerationService>();

// Location Services
// Mapbox Service (primary geocoding)
builder.Services.AddHttpClient<IMapboxService, MapboxService>(client =>
{
    client.BaseAddress = new Uri("https://api.mapbox.com");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// IP-based location detection (for user registration defaults)
builder.Services.AddScoped<ILocationDetectionService, LocationDetectionService>();

// HashId Service for secure ID encoding
builder.Services.AddSingleton<IHashIdService, HashIdService>();

// Background Services
// Only run cleanup service in non-testing environments
if (builder.Environment.EnvironmentName != "Testing")
{
    builder.Services.AddHostedService<RefreshTokenCleanupService>();
}

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Configure FluentValidation to use English language
ValidatorOptions.Global.LanguageManager.Culture = new System.Globalization.CultureInfo("en");

// SignalR Configuration
builder.Services.AddSignalR(options =>
{
    // Development settings
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();

    // Keep-alive interval (default 15 seconds)
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);

    // Client timeout (default 30 seconds)
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);

    // Max message size (default 32KB)
    options.MaximumReceiveMessageSize = 32 * 1024;
});

// Auth Feature Handlers
builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddScoped<VerifyEmailHandler>();
builder.Services.AddScoped<CompleteRegistrationHandler>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<RefreshTokenHandler>();
builder.Services.AddScoped<LogoutHandler>();
builder.Services.AddScoped<CheckAuthHandler>();
builder.Services.AddScoped<ForgotPasswordHandler>();
builder.Services.AddScoped<ResetPasswordHandler>();
builder.Services.AddScoped<API.Features.Auth.ValidateResetToken.ValidateResetTokenHandler>();

// Events Feature Handlers
builder.Services.AddScoped<CreateEventHandler>();
builder.Services.AddScoped<API.Features.Events.BlockParticipant.BlockParticipantHandler>();
builder.Services.AddScoped<API.Features.Events.UnblockParticipant.UnblockParticipantHandler>();
builder.Services.AddScoped<JoinEventHandler>();
builder.Services.AddScoped<LeaveEventHandler>();
builder.Services.AddScoped<DeleteEventHandler>();

// Messages Feature Handlers
builder.Services.AddScoped<API.Features.Messages.SendMessage.SendMessageHandler>();
builder.Services.AddScoped<API.Features.Messages.GetEventMessages.GetEventMessagesHandler>();

builder.Services.AddScoped<GetEventDetailsHandler>();
builder.Services.AddScoped<GetMyEventsHandler>();
builder.Services.AddScoped<GetMyParticipatingEventsHandler>();
builder.Services.AddScoped<GetMyArchivedEventsHandler>();
builder.Services.AddScoped<GetEventEditConstraintsHandler>();
builder.Services.AddScoped<GetEligibleEventsHandler>();
builder.Services.AddScoped<UpdateEventHandler>();
// Users Feature Handlers
builder.Services.AddScoped<API.Features.Users.GetUserProfile.GetUserProfileHandler>();
builder.Services.AddScoped<API.Features.Users.GetProfileEditConstraints.GetProfileEditConstraintsHandler>();
builder.Services.AddScoped<API.Features.Users.UpdateProfile.UpdateProfileHandler>();
builder.Services.AddScoped<API.Features.Users.VerifyUser.VerifyUserHandler>();
builder.Services.AddScoped<API.Features.Users.UpdatePassword.UpdatePasswordHandler>();
builder.Services.AddScoped<API.Features.Users.UpdateEmail.UpdateEmailHandler>();
builder.Services.AddScoped<API.Features.Users.GetUserPreferences.GetUserPreferencesHandler>();
builder.Services.AddScoped<API.Features.Users.UpdatePreferences.UpdatePreferencesHandler>();
builder.Services.AddScoped<API.Features.Users.UpdateLocation.UpdateLocationHandler>();
builder.Services.AddScoped<API.Features.Users.UpdateCategories.UpdateCategoriesHandler>();
builder.Services.AddScoped<API.Features.Users.DeleteAccount.DeleteAccountHandler>();
builder.Services.AddScoped<API.Features.Users.DeleteAccount.DeleteAccountHandler>();

// Locations Feature Handlers
builder.Services.AddScoped<API.Features.Locations.SearchLocation.SearchLocationHandler>();
builder.Services.AddScoped<API.Features.Locations.ReverseGeocode.ReverseGeocodeHandler>();

// Images Feature Handlers
builder.Services.AddScoped<API.Features.Images.UploadImage.UploadImageHandler>();
builder.Services.AddScoped<API.Features.Images.GenerateImage.GenerateImageHandler>();

// Build the app
var app = builder.Build();

// Configure the HTTP request pipeline
// Enable Swagger JSON (needed for Azure API Management import)
app.UseSwagger();

// Only enable Swagger UI in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Meeps API v1");
        options.RoutePrefix = "swagger"; // Move Swagger to /swagger (Blazor uses root)
    });
}

app.UseExceptionHandler();

// Always redirect to HTTPS for security
app.UseHttpsRedirection();

// Security Headers
app.Use(async (context, next) =>
{
    // Content Security Policy - Protect against XSS
    var cspPolicy = "default-src 'self'; " +
                    "script-src 'self' 'unsafe-eval'; " +  // Blazor WASM requires unsafe-eval
                    "script-src-elem 'self' https://unpkg.com; " + // Allow Leaflet from unpkg
                    "style-src 'self' 'unsafe-inline'; " + // MudBlazor requires unsafe-inline
                    "style-src-elem 'self' 'unsafe-inline' https://unpkg.com; " + // Allow Leaflet CSS
                    "img-src 'self' data: https: blob:; " + // Allow map tiles
                    "font-src 'self' data:; " +
                    "frame-ancestors 'none';";

    // Add WebSocket support for hot reload in development
    if (app.Environment.IsDevelopment())
    {
        cspPolicy = cspPolicy.Replace("default-src 'self';",
            "default-src 'self'; connect-src 'self' ws://localhost:* wss://localhost:* https://*.tile.openstreetmap.org;");
    }
    else
    {
        cspPolicy = cspPolicy.Replace("default-src 'self';",
            "default-src 'self'; connect-src 'self' https://*.tile.openstreetmap.org;");
    }

    context.Response.Headers.Append("Content-Security-Policy", cspPolicy);

    // Prevent MIME type sniffing
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

    // Prevent clickjacking
    context.Response.Headers.Append("X-Frame-Options", "DENY");

    // Enable XSS protection
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

    // Control referrer information
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

    // Enforce HTTPS with HSTS (HTTP Strict Transport Security)
    if (!context.Request.IsHttps && !builder.Environment.IsDevelopment())
    {
        context.Response.Headers.Append("Strict-Transport-Security",
            "max-age=31536000; includeSubDomains; preload");
    }

    // Permissions Policy (formerly Feature Policy)
    context.Response.Headers.Append("Permissions-Policy",
        "camera=(), microphone=(), geolocation=(), payment=()");

    await next();
});

// Rate Limiting Middleware - Only in non-Testing environments
if (!app.Environment.EnvironmentName.Equals("Testing"))
{
    app.UseRateLimiter();
}

// BFF Pattern: Serve Blazor WASM static files
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Serve uploaded images from Azure File Share (production) or wwwroot (development)
var imageSettings = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<API.Infrastructure.Configuration.ImageSettings>>().Value;
if (imageSettings.UseAzureFileShare && Directory.Exists(imageSettings.AzureFileSharePath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(imageSettings.AzureFileSharePath),
        RequestPath = "/uploads"
    });
}

app.UseAuthentication();
app.UseMiddleware<API.Common.Middleware.TokenRefreshMiddleware>();
app.UseAuthorization();

// Map SignalR Hub endpoint
app.MapHub<API.Infrastructure.Hubs.ChatHub>("/hubs/chat");

// Map Auth Endpoints
app.MapRegister();
app.MapVerifyEmail();
app.MapCompleteRegistration();
app.MapLogin();
app.MapRefreshToken();
app.MapLogout();
CheckAuthEndpoint.Map(app);
app.MapForgotPassword();
app.MapResetPassword();
app.MapValidateResetToken();

// Map Events Endpoints
app.MapCreateEvent();
app.MapBlockParticipant();
app.MapUnblockParticipant();
app.MapJoinEvent();
app.MapLeaveEvent();
app.MapDeleteEvent();

// Map Messages Endpoints
API.Features.Messages.SendMessage.SendMessageEndpoint.MapSendMessage(app);
API.Features.Messages.GetEventMessages.GetEventMessagesEndpoint.MapGetEventMessages(app);

app.MapGetEventDetails();
app.MapGetEventEditConstraints();
app.MapGetMyEvents();
app.MapGetMyParticipatingEvents();
app.MapGetMyArchivedEvents();
app.MapGetEligibleEvents();
app.MapUpdateEvent();
// Map Users Endpoints
API.Features.Users.GetUserProfile.GetUserProfileEndpoint.Map(app);
API.Features.Users.GetProfileEditConstraints.GetProfileEditConstraintsEndpoint.Map(app);
API.Features.Users.UpdateProfile.UpdateProfileEndpoint.Map(app);
API.Features.Users.VerifyUser.VerifyUserEndpoint.Map(app);
API.Features.Users.UpdatePassword.UpdatePasswordEndpoint.Map(app);
API.Features.Users.UpdateEmail.UpdateEmailEndpoint.Map(app);
API.Features.Users.GetUserPreferences.GetUserPreferencesEndpoint.Map(app);
API.Features.Users.UpdatePreferences.UpdatePreferencesEndpoint.Map(app);
API.Features.Users.UpdateLocation.UpdateLocationEndpoint.Map(app);
API.Features.Users.UpdateCategories.UpdateCategoriesEndpoint.Map(app);
API.Features.Users.DeleteAccount.DeleteAccountEndpoint.Map(app);

// Map Locations Endpoints
API.Features.Locations.SearchLocation.SearchLocationEndpoint.Map(app);
API.Features.Locations.ReverseGeocode.ReverseGeocodeEndpoint.Map(app);

// Map Images Endpoints
API.Features.Images.UploadImage.UploadImageEndpoint.Map(app);
API.Features.Images.GenerateImage.GenerateImageEndpoint.Map(app);

#if DEBUG
// Map Test Endpoints (only in Testing environment)
// Available only in Debug builds to support development and integration tests
if (app.Environment.EnvironmentName == "Testing")
{
    TestEndpoints.Map(app);
}
#endif

// Health check endpoint
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .WithOpenApi();

// BFF Pattern: Fallback to Blazor WASM for all non-API routes
app.MapFallbackToFile("index.html");

// Seed roles
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    await RoleSeeder.SeedRolesAsync(roleManager);
}

app.Run();

// Make Program class accessible to integration tests
namespace API
{
    public partial class Program { }
}
