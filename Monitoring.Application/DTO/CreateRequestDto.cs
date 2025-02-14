using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Application.DTO
{
    public class CreateRequestDto
    {
        public string DocumentNumber { get; set; } = string.Empty;
        public string RequestType { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public string Receiver { get; set; } = string.Empty;

        // Новое поле — какую дату хотим установить
        public DateTime? ProposedDate { get; set; }

        // Заметка
        public string? Note { get; set; }
    }
}
