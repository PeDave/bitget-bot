namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Helper class for centralized interval parsing
/// </summary>
public static class IntervalHelper
{
    /// <summary>
    /// Parse interval string to TimeSpan
    /// </summary>
    /// <param name="interval">Interval string (e.g., "1m", "5m", "1h", "1d")</param>
    /// <returns>TimeSpan representing the interval duration. Returns 1 minute for invalid intervals.</returns>
    public static TimeSpan ParseIntervalToTimeSpan(string interval)
    {
        return interval.ToLowerInvariant() switch
        {
            "1m" => TimeSpan.FromMinutes(1),
            "5m" => TimeSpan.FromMinutes(5),
            "15m" => TimeSpan.FromMinutes(15),
            "30m" => TimeSpan.FromMinutes(30),
            "1h" => TimeSpan.FromHours(1),
            "4h" => TimeSpan.FromHours(4),
            "6h" => TimeSpan.FromHours(6),
            "12h" => TimeSpan.FromHours(12),
            "1d" => TimeSpan.FromDays(1),
            "3d" => TimeSpan.FromDays(3),
            "1w" => TimeSpan.FromDays(7),
            // Note: Using 30 days as approximation for monthly intervals
            "1mo" or "1month" => TimeSpan.FromDays(30),
            _ => TimeSpan.FromMinutes(1) // Default to 1 minute for backward compatibility
        };
    }

    /// <summary>
    /// Get maximum backfill range for an interval to prevent excessive API calls
    /// </summary>
    /// <param name="interval">Interval string</param>
    /// <returns>Maximum backfill range as TimeSpan</returns>
    public static TimeSpan GetMaxBackfillRange(string interval)
    {
        return interval.ToLowerInvariant() switch
        {
            // 1m, 5m: max 6 hours
            "1m" or "5m" => TimeSpan.FromHours(6),
            
            // 15m, 30m, 1h: max 7 days
            "15m" or "30m" or "1h" => TimeSpan.FromDays(7),
            
            // 4h, 6h, 12h: max 30 days
            "4h" or "6h" or "12h" => TimeSpan.FromDays(30),
            
            // 1d, 3d: max 180 days
            "1d" or "3d" => TimeSpan.FromDays(180),
            
            // 1w: max 365 days
            "1w" => TimeSpan.FromDays(365),
            
            // 1mo: max 365 days (or disable gap detection by default)
            "1mo" or "1month" => TimeSpan.FromDays(365),
            
            _ => TimeSpan.FromHours(6) // Default to conservative limit
        };
    }

    /// <summary>
    /// Check if gap detection should be enabled for an interval
    /// Monthly intervals may have special handling requirements
    /// </summary>
    /// <param name="interval">Interval string</param>
    /// <returns>True if gap detection is recommended for this interval</returns>
    public static bool IsGapDetectionRecommended(string interval)
    {
        // For monthly intervals, gap detection can be problematic due to variable month lengths
        // Consider disabling by default or using special handling
        return interval.ToLowerInvariant() switch
        {
            "1mo" or "1month" => false,
            _ => true
        };
    }
}
