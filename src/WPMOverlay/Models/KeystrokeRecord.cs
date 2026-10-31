using System;

namespace WPMOverlay.Models
{
    public struct KeystrokeRecord
    {
        public DateTime Timestamp { get; }
        public bool IsBackspace { get; }
        public int VirtualKey { get; }

        public KeystrokeRecord(DateTime timestamp, bool isBackspace, int virtualKey)
        {
            Timestamp = timestamp;
            IsBackspace = isBackspace;
            VirtualKey = virtualKey;
        }
    }
}
