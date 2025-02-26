namespace Monitoring.Domain.Entities
{
    public class WorkRequest
    {
        public int Id { get; set; }

        // "docNumber/idWork"
        public string WorkDocumentNumber { get; set; } = "";

        // "ТипДок + название"
        public string DocumentName { get; set; } = "";

        // Название работы (если нужно отдельно)
        public string WorkName { get; set; } = "";

        public string RequestType { get; set; } = "";  // "корр1"/"корр2"/"fact" и т.д.
        public string Sender { get; set; } = "";
        public string Receiver { get; set; } = "";
        public DateTime RequestDate { get; set; }
        public bool IsDone { get; set; }
        public string? Note { get; set; }
        public DateTime? ProposedDate { get; set; }
        public string Status { get; set; } = "";       // "Pending" / "Accepted" / "Declined"

        // Поля, чтобы не делать JOIN
        public string Executor { get; set; } = "";
        public string Controller { get; set; } = "";
        public DateTime? PlanDate { get; set; }
        public DateTime? Korrect1 { get; set; }
        public DateTime? Korrect2 { get; set; }
        public DateTime? Korrect3 { get; set; }
    }
}