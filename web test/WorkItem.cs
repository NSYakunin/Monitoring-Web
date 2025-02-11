namespace Monitoring.UI
{
    // Класс модели, описывающей одну строку данных
    public class WorkItem
    {
        public string DocumentNumber { get; set; }
        public string DocumentName { get; set; }
        public string WorkName { get; set; }
        public string Executor { get; set; }
        public string Controller { get; set; }
        public string Approver { get; set; }
        public DateTime? PlanDate { get; set; }
        public DateTime? Korrect1 { get; set; }
        public DateTime? Korrect2 { get; set; }
        public DateTime? Korrect3 { get; set; }
        public DateTime? FactDate { get; set; }

        public DateTime? FactChoiseTime { get; set; }
    }
}
