namespace WinLive.Windows;

internal static class MediaActivityIdentity
{
    public static string SourceKey(string? sourceAppUserModelId, int sessionInstanceKey)
    {
        return string.IsNullOrWhiteSpace(sourceAppUserModelId)
            ? $"session-{sessionInstanceKey:X}"
            : sourceAppUserModelId.Trim();
    }

    public static string SingleActivityId(string sourceKey)
    {
        return $"media:{sourceKey}";
    }

    public static IReadOnlyDictionary<int, string> BuildActivityIds(
        IReadOnlyList<MediaSessionIdentityInput> sessions,
        IReadOnlyDictionary<int, string> existingActivityIds)
    {
        var result = new Dictionary<int, string>();
        foreach (var group in sessions.GroupBy(item => item.SourceKey, StringComparer.Ordinal))
        {
            if (group.Count() == 1)
            {
                var item = group.Single();
                result[item.SessionInstanceKey] = SingleActivityId(item.SourceKey);
                continue;
            }

            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            var nextIndex = 1;
            foreach (var item in group)
            {
                if (existingActivityIds.TryGetValue(item.SessionInstanceKey, out var existingId) &&
                    IsActivityIdForSource(existingId, item.SourceKey) &&
                    usedIds.Add(existingId))
                {
                    result[item.SessionInstanceKey] = existingId;
                    continue;
                }

                string id;
                do
                {
                    id = nextIndex == 1
                        ? SingleActivityId(item.SourceKey)
                        : $"{SingleActivityId(item.SourceKey)}:{nextIndex - 1}";
                    nextIndex++;
                }
                while (!usedIds.Add(id));

                result[item.SessionInstanceKey] = id;
            }
        }

        return result;
    }

    private static bool IsActivityIdForSource(string activityId, string sourceKey)
    {
        var baseId = SingleActivityId(sourceKey);
        return string.Equals(activityId, baseId, StringComparison.Ordinal) ||
            activityId.StartsWith($"{baseId}:", StringComparison.Ordinal);
    }
}

internal readonly record struct MediaSessionIdentityInput(
    int SessionInstanceKey,
    string SourceKey);
