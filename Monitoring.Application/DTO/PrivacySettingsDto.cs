using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Application.DTO
{
    // DTO для хранения privacy-настроек
    public class PrivacySettingsDto
    {
        public bool CanCloseWork { get; set; }
        public bool CanSendCloseRequest { get; set; }
        public bool CanAccessSettings { get; set; }
    }
}
