using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Application.DTO
{
    // Для удаления заявки
    public class DeleteRequestDto
    {
        public int RequestId { get; set; }
        public string DocumentNumber { get; set; } = "";
    }
}
