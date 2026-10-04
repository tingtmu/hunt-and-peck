using System;
using System.Collections.Generic;

namespace HuntAndPeck.Diagnostics
{
    /// <summary>
    /// Detects more than a given number of events within a sliding time window
    /// </summary>
    internal sealed class BurstDetector
    {
        private readonly int _maxEvents;
        private readonly TimeSpan _window;
        private readonly Queue<DateTime> _events = new Queue<DateTime>();
        private readonly object _lock = new object();

        /// <param name="maxEvents">Events allowed within the window; one more is a burst</param>
        /// <param name="window">The sliding window</param>
        public BurstDetector(int maxEvents, TimeSpan window)
        {
            _maxEvents = maxEvents;
            _window = window;
        }

        /// <summary>
        /// Records an event
        /// </summary>
        /// <returns>True if more than the allowed number of events happened within the window ending now</returns>
        public bool Record(DateTime utcNow)
        {
            lock (_lock)
            {
                _events.Enqueue(utcNow);
                while (_events.Count > 0 && utcNow - _events.Peek() >= _window)
                {
                    _events.Dequeue();
                }
                return _events.Count > _maxEvents;
            }
        }
    }
}
