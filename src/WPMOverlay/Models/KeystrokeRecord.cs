namespace WPMOverlay.Models
{
    /// <summary>
    /// Minimal keystroke sample retained in the rolling WPM buffer.
    /// Intentionally omits VirtualKey so no raw keystroke sequence is retained in RAM.
    /// </summary>
    public readonly record struct KeystrokeRecord(long TimestampTicks, bool IsBackspace);
}
