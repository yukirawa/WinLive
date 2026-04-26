namespace WinLive.Core;

public sealed class CompositeLiveActivityCommandRouter : ILiveActivityCommandRouter
{
    private readonly IReadOnlyList<ILiveActivityCommandRouter> _routers;

    public CompositeLiveActivityCommandRouter(IEnumerable<ILiveActivityCommandRouter> routers)
    {
        _routers = routers.ToArray();
    }

    public bool CanExecute(string activityId, LiveActivityActionKind action)
    {
        return _routers.Any(router => router.CanExecute(activityId, action));
    }

    public async Task ExecuteAsync(
        string activityId,
        LiveActivityActionKind action,
        CancellationToken cancellationToken = default)
    {
        foreach (var router in _routers)
        {
            if (router.CanExecute(activityId, action))
            {
                await router.ExecuteAsync(activityId, action, cancellationToken).ConfigureAwait(false);
                return;
            }
        }
    }
}
