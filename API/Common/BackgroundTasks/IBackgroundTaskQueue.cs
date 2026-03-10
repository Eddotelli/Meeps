namespace API.Common.BackgroundTasks;

public interface IBackgroundTaskQueue
{
    void QueueMessageModeration(int messageId);
    ValueTask<int> DequeueAsync(CancellationToken cancellationToken);
}
