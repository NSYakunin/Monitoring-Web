// Monitoring.Application/Services/ReportGenerator.cs
using Monitoring.Domain.Entities;

namespace Monitoring.Application.Services
{
    internal class ReportGenerator
    /// <summary>
    /// Утилитный класс, который формирует PDF-файл (или другой отчет)
    /// на основе списка WorkItem.
    /// </summary>
    public static class ReportGenerator
    {
        public static byte[] GeneratePdf(List<WorkItem> items, string title, string dev)
    {
            // Здесь твой код, который вызывает QuestPDF 
            // и формирует PDF в памяти. 
            // Важно, что он не знает, КАК мы доставали данные (SQL, EF, ...).
            // Он просто принимает готовый список WorkItem.

            // ... Твой код генерации ...
            // return pdfBytes;

            return Array.Empty<byte>(); // заглушка
        }
    }
}
