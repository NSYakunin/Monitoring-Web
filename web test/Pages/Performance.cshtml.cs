using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitoring.Application.Interfaces;
using System;
using System.Collections.Generic;

namespace Monitoring.UI.Pages
{
    /// <summary>
    /// Модель Razor-страницы для отображения отчёта по подразделениям.
    /// </summary>
    public class PerformanceModel : PageModel
    {
        private readonly IPerformanceService _performanceService;

        // Конструктор, принимающий сервис через DI
        public PerformanceModel(IPerformanceService performanceService)
        {
            _performanceService = performanceService;
        }

        /// <summary>
        /// Дата начала периода (приходит с формы).
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Дата окончания периода (приходит с формы).
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Список результатов для отображения.
        /// </summary>
        public List<PerformanceDto> Results { get; set; } = new();

        /// <summary>
        /// Обработка GET-запроса.
        /// </summary>
        public void OnGet()
        {
            // Если даты не указаны, берем "с 1-го числа текущего месяца" и "по сегодня".
            if (StartDate == default)
                StartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (EndDate == default)
                EndDate = DateTime.Now.Date;

            // Вызываем наш сервис, который вернёт список данных из БД.
            Results = _performanceService.GetPerformanceData(StartDate, EndDate);
        }
    }
}