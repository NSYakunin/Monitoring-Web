using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitoring.Application.Interfaces;
using System;
using System.Collections.Generic;

namespace Monitoring.UI.Pages
{
    /// <summary>
    /// ������ Razor-�������� ��� ����������� ������ �� ��������������.
    /// </summary>
    public class PerformanceModel : PageModel
    {
        private readonly IPerformanceService _performanceService;

        // �����������, ����������� ������ ����� DI
        public PerformanceModel(IPerformanceService performanceService)
        {
            _performanceService = performanceService;
        }

        /// <summary>
        /// ���� ������ ������� (�������� � �����).
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public DateTime StartDate { get; set; }

        /// <summary>
        /// ���� ��������� ������� (�������� � �����).
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public DateTime EndDate { get; set; }

        /// <summary>
        /// ������ ����������� ��� �����������.
        /// </summary>
        public List<PerformanceDto> Results { get; set; } = new();

        /// <summary>
        /// ��������� GET-�������.
        /// </summary>
        public void OnGet()
        {
            // ���� ���� �� �������, ����� "� 1-�� ����� �������� ������" � "�� �������".
            if (StartDate == default)
                StartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (EndDate == default)
                EndDate = DateTime.Now.Date;

            // �������� ��� ������, ������� ����� ������ ������ �� ��.
            Results = _performanceService.GetPerformanceData(StartDate, EndDate);
        }
    }
}