using Client.Services;
using Client.Services.ApiClients;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using MudBlazor;
using LeafletForBlazor;
using Blazored.LocalStorage;

namespace Client;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        // Configure HttpClient for API calls (BFF Pattern - same origin)
        // Note: Cookies are automatically handled by the browser in Blazor WebAssembly
        // as long as CORS is configured with AllowCredentials (which it is)
        builder.Services.AddScoped(sp => new HttpClient
        {
            BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) // Same origin as host
        });

        // Register I18n service (uses same HttpClient as above)
        builder.Services.AddScoped<II18nService, I18nService>();

        // Register Blazored.LocalStorage
        builder.Services.AddBlazoredLocalStorage();

        // Register distance unit service
        builder.Services.AddScoped<IDistanceUnitService, DistanceUnitService>();

        // Register event filter service (singleton for shared state)
        builder.Services.AddSingleton<IEventFilterService, EventFilterService>();

        // Register event filter state service (scoped for LocalStorage)
        builder.Services.AddScoped<IEventFilterStateService, EventFilterStateService>();

        // Register validation error mapper
        builder.Services.AddScoped<ValidationErrorMapper>();

        // Register moderation alert service
        builder.Services.AddScoped<ModerationAlertService>();

        // Register API clients
        builder.Services.AddScoped<ApiClient>();
        builder.Services.AddScoped<IAuthApiClient, AuthApiClient>();
        builder.Services.AddScoped<IEventsApiClient, EventsApiClient>();
        builder.Services.AddScoped<IUsersApiClient, UsersApiClient>();
        builder.Services.AddScoped<ILocationsApiClient, LocationsApiClient>();
        builder.Services.AddScoped<IMessagesApiClient, MessagesApiClient>();
        builder.Services.AddScoped<IImagesApiClient, ImagesApiClient>();

        // Register authentication services
        builder.Services.AddScoped<CustomAuthenticationStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
            provider.GetRequiredService<CustomAuthenticationStateProvider>());
        builder.Services.AddAuthorizationCore();

        // Add MudBlazor services with Snackbar configuration
        builder.Services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
            config.SnackbarConfiguration.PreventDuplicates = false;
            config.SnackbarConfiguration.NewestOnTop = false;
            config.SnackbarConfiguration.ShowCloseIcon = true;
            config.SnackbarConfiguration.VisibleStateDuration = 4000;
            config.SnackbarConfiguration.HideTransitionDuration = 500;
            config.SnackbarConfiguration.ShowTransitionDuration = 500;
        });

        // Register SignalR service as Singleton (shared connection)
        builder.Services.AddSingleton<IChatSignalRService, ChatSignalRService>();

        var app = builder.Build();

        // Initialize I18n service
        var i18n = app.Services.GetRequiredService<II18nService>();
        await i18n.InitializeAsync();

        // Initialize SignalR connection if user is authenticated
        var authStateProvider = app.Services
            .GetRequiredService<AuthenticationStateProvider>();
        var authState = await authStateProvider.GetAuthenticationStateAsync();

        if (authState.User.Identity?.IsAuthenticated == true)
        {
            var signalRService = app.Services
                .GetRequiredService<IChatSignalRService>();

            var logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger<Program>();

            try
            {
                await signalRService.StartAsync();
                logger.LogInformation("SignalR connection initialized");
            }
            catch (Exception ex)
            {
                // Log but don't fail app startup if SignalR fails
                logger.LogError(ex, "Failed to start SignalR connection");
            }
        }

        await app.RunAsync();
    }
}
