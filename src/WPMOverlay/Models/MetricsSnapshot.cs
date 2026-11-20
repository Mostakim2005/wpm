namespace WPMOverlay.Models
{
    public readonly record struct MetricsSnapshot(
        bool IsTypingActive,
        double Current15sWpm,
        double Current30sWpm,
        double Current60sWpm,
        double Current5mWpm,
        double CurrentAccuracy,
        double LastFinishedAvgWpm,
        double LastFinished15sWpm,
        double LastFinished30sWpm,
        double LastFinished60sWpm,
        double LastFinished5mWpm,
        double LastFinishedAccuracy,
        StatsSummary Summary);
}
