using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Application.DTO
{
    public class StatusChangeDto
    {
        public int RequestId { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty; // "Accepted" / "Declined"
    }
}
