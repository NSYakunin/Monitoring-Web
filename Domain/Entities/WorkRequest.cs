namespace Monitoring.Domain.Entities
{
    /// <summary>
    /// Заявка/запрос на изменение дат (корр1/2/3/факт) в WorkItem.
    /// </summary>
    public class WorkRequest
    {
        public int Id { get; set; }

        // Связка с WorkItem.DocumentNumber
        public string WorkDocumentNumber { get; set; } = string.Empty;

        // Тип запроса: "корр1", "корр2", "корр3", "fact"
        public string RequestType { get; set; } = string.Empty;

        // Отправитель (должен быть один из исполнителей)
        public string Sender { get; set; } = string.Empty;

        // Получатель (Controller или Approver)
        public string Receiver { get; set; } = string.Empty;

        // Дата, когда заявку создали
        public DateTime RequestDate { get; set; }

        // Предлагаемая дата, которую хотим установить
        public DateTime? ProposedDate { get; set; }

        // Текущий статус: "Pending", "Accepted", "Declined"
        public string Status { get; set; } = "Pending";

        // Флаг, что заявка полностью завершена (принята/отклонена)
        public bool IsDone { get; set; } = false;

        // Доп. заметка
        public string? Note { get; set; }

        public string? DocumentName { get; set; }
        public string? WorkName { get; set; }

        public string? Executor { get; set; }
        public string? Controller { get; set; }
        public string? Approver { get; set; }
        public DateTime? PlanDate { get; set; }
        public DateTime? Korrect1 { get; set; }
        public DateTime? Korrect2 { get; set; }
        public DateTime? Korrect3 { get; set; }
    }
}