using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Application.DTO
{
    // Для обновления заявки
    public class UpdateRequestDto
    {
        public int Id { get; set; }
        public string DocumentNumber { get; set; } = "";
        public string RequestType { get; set; } = "";
        public string Receiver { get; set; } = "";
        public DateTime? ProposedDate { get; set; }
        public string? Note { get; set; }
    }
}
