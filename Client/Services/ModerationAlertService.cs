using MudBlazor;
using Shared.Contracts.Messages;

namespace Client.Services;

public class ModerationAlertService
{
    private readonly ISnackbar _snackbar;
    private readonly II18nService _i18n;

    public ModerationAlertService(ISnackbar snackbar, II18nService i18n)
    {
        _snackbar = snackbar;
        _i18n = i18n;
    }

    public Task ShowBlockedAlert(SendMessageResponse response)
    {
        var message = $"🚫 {_i18n.GetMessage("moderationBlockedMessage")}";

        _snackbar.Add(message, Severity.Error, config =>
        {
            config.VisibleStateDuration = 6000; // 6 seconds
            config.HideTransitionDuration = 500;
            config.ShowTransitionDuration = 500;
            config.ShowCloseIcon = true;
            config.SnackbarVariant = Variant.Filled;
        });

        return Task.CompletedTask;
    }

    public Task ShowWarningAlert(SendMessageResponse response)
    {
        var message = $"⚠️ {_i18n.GetMessage("moderationWarningMessage")}";

        _snackbar.Add(message, Severity.Warning, config =>
        {
            config.VisibleStateDuration = 5000; // 5 seconds
            config.HideTransitionDuration = 500;
            config.ShowTransitionDuration = 500;
            config.ShowCloseIcon = true;
            config.SnackbarVariant = Variant.Filled;
        });

        return Task.CompletedTask;
    }
}
