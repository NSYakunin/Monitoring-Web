using System;
using System.Collections.Generic;

namespace Monitoring.Application.Interfaces
{
    /// <summary>
    /// Интерфейс для получения данных об исполнении по подразделениям.
    /// </summary>
    public interface IPerformanceService
    {
        /// <summary>
        /// Возвращает список результатов по исполнению за период.
        /// </summary>
        /// <param name="startDate">Дата начала периода</param>
        /// <param name="endDate">Дата окончания периода</param>
        /// <returns>Список DTO с данными о плане, факте и проценте исполнения.</returns>
        List<PerformanceDto> GetPerformanceData(DateTime startDate, DateTime endDate);
    }

    /// <summary>
    /// DTO для данных об исполнении: подразделение, план, факт, % и т.д.
    /// </summary>
    public class PerformanceDto
    {
        public int DivisionId { get; set; }
        public string DivisionName { get; set; } = string.Empty;
        public int PlanCount { get; set; }
        public int FactCount { get; set; }
        /// <summary>
        /// Процент исполнения в виде десятичной дроби (0.25 = 25%).
        /// </summary>
        public decimal Percentage { get; set; }
    }
}