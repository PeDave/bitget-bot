using BitgetLab.Core.Models;
using System.Collections.Concurrent;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Thread-safe ring buffer for storing a fixed number of candles
/// </summary>
public class CandleBuffer
{
    private readonly int _maxSize;
    private readonly ConcurrentQueue<CandleDto> _buffer = new();
    private readonly object _lock = new();

    public CandleBuffer(int maxSize = 500)
    {
        if (maxSize <= 0)
        {
            throw new ArgumentException("Max size must be greater than 0", nameof(maxSize));
        }
        _maxSize = maxSize;
    }

    /// <summary>
    /// Add or update a candle in the buffer
    /// </summary>
    /// <param name="candle">The candle to add or update</param>
    public void AddOrUpdate(CandleDto candle)
    {
        lock (_lock)
        {
            // Check if we need to update the last candle (same OpenTime)
            var bufferList = _buffer.ToList();
            if (bufferList.Count > 0 && bufferList[^1].OpenTime == candle.OpenTime)
            {
                // Replace the last candle
                var newBuffer = new ConcurrentQueue<CandleDto>(bufferList.Take(bufferList.Count - 1));
                newBuffer.Enqueue(candle);
                
                // Replace the entire buffer
                _buffer.Clear();
                foreach (var c in newBuffer)
                {
                    _buffer.Enqueue(c);
                }
            }
            else
            {
                // Add new candle
                _buffer.Enqueue(candle);
                
                // Trim to max size
                while (_buffer.Count > _maxSize)
                {
                    _buffer.TryDequeue(out _);
                }
            }
        }
    }

    /// <summary>
    /// Add multiple candles to the buffer (for backfill)
    /// </summary>
    /// <param name="candles">Candles to add, should be in chronological order</param>
    public void AddRange(IEnumerable<CandleDto> candles)
    {
        lock (_lock)
        {
            var candlesList = candles.OrderBy(c => c.OpenTime).ToList();
            var bufferList = _buffer.ToList();
            
            // Merge candles, avoiding duplicates by OpenTime
            var existingOpenTimes = new HashSet<DateTime>(bufferList.Select(c => c.OpenTime));
            
            foreach (var candle in candlesList)
            {
                if (!existingOpenTimes.Contains(candle.OpenTime))
                {
                    bufferList.Add(candle);
                    existingOpenTimes.Add(candle.OpenTime);
                }
            }
            
            // Sort and trim
            bufferList = bufferList.OrderBy(c => c.OpenTime).ToList();
            if (bufferList.Count > _maxSize)
            {
                bufferList = bufferList.Skip(bufferList.Count - _maxSize).ToList();
            }
            
            // Replace the entire buffer
            _buffer.Clear();
            foreach (var c in bufferList)
            {
                _buffer.Enqueue(c);
            }
        }
    }

    /// <summary>
    /// Get candles from the buffer
    /// </summary>
    /// <param name="limit">Maximum number of candles to return (from most recent)</param>
    /// <returns>Candles in chronological order</returns>
    public List<CandleDto> GetCandles(int? limit = null)
    {
        lock (_lock)
        {
            var bufferList = _buffer.ToList();
            
            if (limit.HasValue && limit.Value > 0 && limit.Value < bufferList.Count)
            {
                // Return the last N candles (most recent)
                return bufferList.Skip(bufferList.Count - limit.Value).ToList();
            }
            
            return bufferList;
        }
    }

    /// <summary>
    /// Get the last (most recent) candle in the buffer
    /// </summary>
    public CandleDto? GetLastCandle()
    {
        lock (_lock)
        {
            var bufferList = _buffer.ToList();
            return bufferList.Count > 0 ? bufferList[^1] : null;
        }
    }

    /// <summary>
    /// Get the count of candles in the buffer
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _buffer.Count;
            }
        }
    }
}
