using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Monitoring.Domain/Entities/WorkItem.cs
namespace Monitoring.Domain.Entities
{
    /// <summary>
    /// Бизнес-сущность (Entity) "WorkItem".
    /// Описывает задачу (работу), которую выполняет пользователь.
    /// </summary>
    public class WorkItem
    {
        public string DocumentNumber { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        public string WorkName { get; set; } = string.Empty;

        /// <summary>
        /// Исполнитель. Для простоты - строка (smallName).
        /// </summary>
        public string Executor { get; set; } = string.Empty;

        public string Controller { get; set; } = string.Empty;
        public string Approver { get; set; } = string.Empty;

        public DateTime? PlanDate { get; set; }
        public DateTime? Korrect1 { get; set; }
        public DateTime? Korrect2 { get; set; }
        public DateTime? Korrect3 { get; set; }
        public DateTime? FactDate { get; set; }

        public string HighlightCssClass { get; set; } = "";

        // Для хранения Pending-заявки от текущего пользователя
        public int? UserPendingRequestId { get; set; }
        public string? UserPendingRequestType { get; set; }
        public DateTime? UserPendingProposedDate { get; set; }
        public string? UserPendingRequestNote { get; set; }
        public string? UserPendingReceiver { get; set; }
    }
}
