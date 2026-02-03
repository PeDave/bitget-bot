using BitgetLab.Core.Models;
using System.Collections.Concurrent;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Thread-safe fixed-size ring buffer for candles
/// </summary>
public class CandleRingBuffer
{
    private readonly int _maxSize;
    private readonly List<CandleDto> _buffer;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

    public CandleRingBuffer(int maxSize = 500)
    {
        if (maxSize <= 0)
        {
            throw new ArgumentException("Buffer size must be positive", nameof(maxSize));
        }

        _maxSize = maxSize;
        _buffer = new List<CandleDto>(maxSize);
    }

    /// <summary>
    /// Add or update a candle in the buffer
    /// If the candle's OpenTime matches the last candle, it updates; otherwise appends
    /// </summary>
    public void AddOrUpdate(CandleDto candle)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_buffer.Count > 0)
            {
                var lastCandle = _buffer[^1];
                
                // If same OpenTime, update the last candle
                if (lastCandle.OpenTime == candle.OpenTime)
                {
                    _buffer[^1] = candle;
                    return;
                }
                
                // If new candle is older than the last, we need to insert or ignore
                if (candle.OpenTime < lastCandle.OpenTime)
                {
                    // Find insertion point or update existing
                    int index = _buffer.BinarySearch(candle, Comparer<CandleDto>.Create((a, b) => a.OpenTime.CompareTo(b.OpenTime)));
                    
                    if (index >= 0)
                    {
                        // Found exact match, update
                        _buffer[index] = candle;
                    }
                    else
                    {
                        // Not found, insert at correct position
                        index = ~index;
                        _buffer.Insert(index, candle);
                        
                        // Trim if exceeds max size (remove oldest)
                        if (_buffer.Count > _maxSize)
                        {
                            _buffer.RemoveAt(0);
                        }
                    }
                    return;
                }
            }
            
            // Append new candle (most common case)
            _buffer.Add(candle);
            
            // Trim if exceeds max size (remove oldest)
            if (_buffer.Count > _maxSize)
            {
                _buffer.RemoveAt(0);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Add multiple candles in bulk (e.g., from backfill)
    /// </summary>
    public void AddRange(IEnumerable<CandleDto> candles)
    {
        foreach (var candle in candles.OrderBy(c => c.OpenTime))
        {
            AddOrUpdate(candle);
        }
    }

    /// <summary>
    /// Get all candles in chronological order
    /// </summary>
    public List<CandleDto> GetAll()
    {
        _lock.EnterReadLock();
        try
        {
            return new List<CandleDto>(_buffer);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Get the latest N candles
    /// </summary>
    public List<CandleDto> GetLatest(int count)
    {
        _lock.EnterReadLock();
        try
        {
            if (count >= _buffer.Count)
            {
                return new List<CandleDto>(_buffer);
            }

            return _buffer.Skip(_buffer.Count - count).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Get the most recent candle
    /// </summary>
    public CandleDto? GetLatestCandle()
    {
        _lock.EnterReadLock();
        try
        {
            return _buffer.Count > 0 ? _buffer[^1] : null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Get current count of candles in buffer
    /// </summary>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _buffer.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Clear all candles from buffer
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _buffer.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Detect gaps between consecutive candles
    /// Returns list of gap ranges (start time, end time)
    /// </summary>
    public List<(DateTime Start, DateTime End)> DetectGaps(string interval)
    {
        _lock.EnterReadLock();
        try
        {
            var gaps = new List<(DateTime Start, DateTime End)>();
            
            if (_buffer.Count < 2)
            {
                return gaps;
            }

            var expectedDuration = ParseIntervalToTimeSpan(interval);

            for (int i = 1; i < _buffer.Count; i++)
            {
                var prevCandle = _buffer[i - 1];
                var currentCandle = _buffer[i];
                var timeDiff = currentCandle.OpenTime - prevCandle.OpenTime;

                // If gap is more than expected interval, record it
                if (timeDiff > expectedDuration)
                {
                    gaps.Add((prevCandle.OpenTime + expectedDuration, currentCandle.OpenTime));
                }
            }

            return gaps;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private TimeSpan ParseIntervalToTimeSpan(string interval)
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
            "1mo" or "1month" => TimeSpan.FromDays(30),
            _ => TimeSpan.FromMinutes(1) // Default to 1 minute
        };
    }
}
