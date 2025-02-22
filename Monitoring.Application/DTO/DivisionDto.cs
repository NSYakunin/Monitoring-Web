using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Application.DTO
{
    // DTO для "подразделений"
    public class DivisionDto
    {
        public int IdDivision { get; set; }
        public int? IdParentDivision { get; set; }
        public string NameDivision { get; set; } = "";
        public string SmallNameDivision { get; set; } = "";
        public int? Position { get; set; }
        public int? IdUserHead { get; set; }
    }
}
