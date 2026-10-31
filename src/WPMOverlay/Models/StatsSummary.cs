namespace WPMOverlay.Models
{
    public class StatsSummary
    {
        public double AllTimeTopWpm { get; set; }
        public double TodayTopWpm { get; set; }
        public double OverallAvgWpm { get; set; }
        public double LastSessionWpm { get; set; }
        public double LastSession15sWpm { get; set; }
        public double LastSession30sWpm { get; set; }
        public double LastSession60sWpm { get; set; }
        public double TotalWordsTyped { get; set; }
        public int TotalSessions { get; set; }
    }
}
