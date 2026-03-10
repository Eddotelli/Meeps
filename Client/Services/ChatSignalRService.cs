using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using Shared.Contracts.Messages;

namespace Client.Services;

public class ChatSignalRService : IChatSignalRService, IAsyncDisposable
{
    private readonly ILogger<ChatSignalRService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly NavigationManager _navigationManager;
    private HubConnection? _hubConnection;
    private readonly Dictionary<int, List<Func<MessageDto, Task>>> _eventHandlers = new();
    private readonly Dictionary<int, List<Func<int, string, Task>>> _messageDeletedHandlers = new();
    private readonly Dictionary<int, List<Func<int, int, string, Task>>> _messageFlaggedHandlers = new();
    private readonly Dictionary<int, List<Func<int, string, Task>>> _userTypingHandlers = new();
    private readonly Dictionary<int, List<Func<int, Task>>> _userStoppedTypingHandlers = new();

    public bool IsConnected =>
        _hubConnection?.State == HubConnectionState.Connected;

    public event EventHandler<bool>? ConnectionStateChanged;

    public ChatSignalRService(
        ILogger<ChatSignalRService> logger,
        IServiceProvider serviceProvider,
        NavigationManager navigationManager)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _navigationManager = navigationManager;
    }

    public async Task StartAsync()
    {
        if (_hubConnection != null)
        {
            _logger.LogInformation("SignalR connection already exists");
            return;
        }

        try
        {
            // Create a scope to resolve scoped services from singleton
            using var scope = _serviceProvider.CreateScope();
            var authStateProvider = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();

            // Get JWT token from authentication state
            var authState = await authStateProvider.GetAuthenticationStateAsync();
            var isAuthenticated = authState.User.Identity?.IsAuthenticated ?? false;

            if (!isAuthenticated)
            {
                _logger.LogWarning("User not authenticated, skipping SignalR connection");
                return;
            }

            // Build hub URL
            var baseUrl = _navigationManager.BaseUri.TrimEnd('/');
            var hubUrl = $"{baseUrl}/hubs/chat";

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.Zero,           // Retry immediately
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30)
                })
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .Build();

            // Register handlers
            _hubConnection.On<int, MessageDto>("ReceiveMessage", async (eventId, message) =>
            {
                _logger.LogInformation(
                    "Received message {MessageId} for event {EventId}",
                    message.MessageId, eventId);

                // Invoke all handlers registered for this event
                if (_eventHandlers.TryGetValue(eventId, out var handlers))
                {
                    foreach (var handler in handlers)
                    {
                        try
                        {
                            await handler(message);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Error invoking message handler for event {EventId}",
                                eventId);
                        }
                    }
                }
            });

            // Register MessageDeleted handler
            _hubConnection.On<int, int, string>("MessageDeleted", async (eventId, messageId, reason) =>
            {
                _logger.LogInformation(
                    "Message {MessageId} deleted by moderation in event {EventId}: {Reason}",
                    messageId, eventId, reason);

                if (_messageDeletedHandlers.TryGetValue(eventId, out var handlers))
                {
                    foreach (var handler in handlers)
                    {
                        try
                        {
                            await handler(messageId, reason);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Error invoking MessageDeleted handler for event {EventId}",
                                eventId);
                        }
                    }
                }
            });

            // Register MessageFlagged handler
            _hubConnection.On<int, int, int, string>("MessageFlagged", async (eventId, messageId, userId, warning) =>
            {
                _logger.LogInformation(
                    "Message {MessageId} flagged for user {UserId} in event {EventId}: {Warning}",
                    messageId, userId, eventId, warning);

                if (_messageFlaggedHandlers.TryGetValue(eventId, out var handlers))
                {
                    foreach (var handler in handlers)
                    {
                        try
                        {
                            await handler(messageId, userId, warning);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Error invoking MessageFlagged handler for event {EventId}",
                                eventId);
                        }
                    }
                }
            });

            // Register UserTyping handler
            _hubConnection.On<int, int, string>("UserTyping", async (eventId, userId, userName) =>
            {
                _logger.LogDebug(
                    "User {UserId} ({UserName}) is typing in event {EventId}",
                    userId, userName, eventId);

                if (_userTypingHandlers.TryGetValue(eventId, out var handlers))
                {
                    foreach (var handler in handlers)
                    {
                        try
                        {
                            await handler(userId, userName);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Error invoking UserTyping handler for event {EventId}",
                                eventId);
                        }
                    }
                }
            });

            // Register UserStoppedTyping handler
            _hubConnection.On<int, int>("UserStoppedTyping", async (eventId, userId) =>
            {
                _logger.LogDebug(
                    "User {UserId} stopped typing in event {EventId}",
                    userId, eventId);

                if (_userStoppedTypingHandlers.TryGetValue(eventId, out var handlers))
                {
                    foreach (var handler in handlers)
                    {
                        try
                        {
                            await handler(userId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                "Error invoking UserStoppedTyping handler for event {EventId}",
                                eventId);
                        }
                    }
                }
            });

            // Connection lifecycle events
            _hubConnection.Closed += async (error) =>
            {
                if (error != null)
                {
                    _logger.LogError(error, "SignalR connection closed with error");
                }
                else
                {
                    _logger.LogInformation("SignalR connection closed");
                }

                ConnectionStateChanged?.Invoke(this, false);
                await Task.CompletedTask;
            };

            _hubConnection.Reconnecting += (error) =>
            {
                _logger.LogWarning(error, "SignalR reconnecting...");
                ConnectionStateChanged?.Invoke(this, false);
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += (connectionId) =>
            {
                _logger.LogInformation(
                    "SignalR reconnected with connection ID: {ConnectionId}",
                    connectionId);
                ConnectionStateChanged?.Invoke(this, true);
                return Task.CompletedTask;
            };

            // Start connection
            await _hubConnection.StartAsync();
            _logger.LogInformation("SignalR connection started successfully");
            ConnectionStateChanged?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR connection");
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync();
                _logger.LogInformation("SignalR connection stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping SignalR connection");
            }
        }
    }

    public async Task JoinEventChatAsync(int eventId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning(
                "Cannot join event chat {EventId} - not connected",
                eventId);
            throw new InvalidOperationException("SignalR not connected");
        }

        try
        {
            await _hubConnection.InvokeAsync("JoinEventChat", eventId);
            _logger.LogInformation("Joined event chat {EventId}", eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join event chat {EventId}", eventId);
            throw;
        }
    }

    public async Task LeaveEventChatAsync(int eventId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning(
                "Cannot leave event chat {EventId} - not connected",
                eventId);
            return; // Don't throw on leave
        }

        try
        {
            await _hubConnection.InvokeAsync("LeaveEventChat", eventId);
            _logger.LogInformation("Left event chat {EventId}", eventId);

            // Remove handlers for this event
            _eventHandlers.Remove(eventId);
            _messageDeletedHandlers.Remove(eventId);
            _messageFlaggedHandlers.Remove(eventId);
            _userTypingHandlers.Remove(eventId);
            _userStoppedTypingHandlers.Remove(eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave event chat {EventId}", eventId);
        }
    }

    public IDisposable OnMessageReceived(int eventId, Func<MessageDto, Task> handler)
    {
        if (!_eventHandlers.ContainsKey(eventId))
        {
            _eventHandlers[eventId] = new List<Func<MessageDto, Task>>();
        }

        _eventHandlers[eventId].Add(handler);

        // Return disposable that removes handler
        return new ActionDisposable(() =>
        {
            if (_eventHandlers.TryGetValue(eventId, out var handlers))
            {
                handlers.Remove(handler);
            }
        });
    }

    public IDisposable OnMessageDeleted(int eventId, Func<int, string, Task> handler)
    {
        if (!_messageDeletedHandlers.ContainsKey(eventId))
        {
            _messageDeletedHandlers[eventId] = new List<Func<int, string, Task>>();
        }

        _messageDeletedHandlers[eventId].Add(handler);

        return new ActionDisposable(() =>
        {
            if (_messageDeletedHandlers.TryGetValue(eventId, out var handlers))
            {
                handlers.Remove(handler);
            }
        });
    }

    public IDisposable OnMessageFlagged(int eventId, Func<int, int, string, Task> handler)
    {
        if (!_messageFlaggedHandlers.ContainsKey(eventId))
        {
            _messageFlaggedHandlers[eventId] = new List<Func<int, int, string, Task>>();
        }

        _messageFlaggedHandlers[eventId].Add(handler);

        return new ActionDisposable(() =>
        {
            if (_messageFlaggedHandlers.TryGetValue(eventId, out var handlers))
            {
                handlers.Remove(handler);
            }
        });
    }

    public IDisposable OnUserTyping(int eventId, Func<int, string, Task> handler)
    {
        if (!_userTypingHandlers.ContainsKey(eventId))
        {
            _userTypingHandlers[eventId] = new List<Func<int, string, Task>>();
        }

        _userTypingHandlers[eventId].Add(handler);

        return new ActionDisposable(() =>
        {
            if (_userTypingHandlers.TryGetValue(eventId, out var handlers))
            {
                handlers.Remove(handler);
            }
        });
    }

    public IDisposable OnUserStoppedTyping(int eventId, Func<int, Task> handler)
    {
        if (!_userStoppedTypingHandlers.ContainsKey(eventId))
        {
            _userStoppedTypingHandlers[eventId] = new List<Func<int, Task>>();
        }

        _userStoppedTypingHandlers[eventId].Add(handler);

        return new ActionDisposable(() =>
        {
            if (_userStoppedTypingHandlers.TryGetValue(eventId, out var handlers))
            {
                handlers.Remove(handler);
            }
        });
    }

    public async Task NotifyTypingAsync(int eventId, string userName)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            return;
        }

        try
        {
            await _hubConnection.InvokeAsync("NotifyTyping", eventId, userName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify typing for event {EventId}", eventId);
        }
    }

    public async Task NotifyStoppedTypingAsync(int eventId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            return;
        }

        try
        {
            await _hubConnection.InvokeAsync("NotifyStoppedTyping", eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify stopped typing for event {EventId}", eventId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    // Helper class for disposable subscriptions
    private class ActionDisposable : IDisposable
    {
        private readonly Action _action;

        public ActionDisposable(Action action)
        {
            _action = action;
        }

        public void Dispose() => _action();
    }
}
