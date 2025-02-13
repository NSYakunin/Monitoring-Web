using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitoring.Application.Interfaces;
using Monitoring.Application.Services;
using Monitoring.Domain.Entities;
using System.Text.Json; // Для JsonSerializer

namespace Monitoring.UI.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IWorkItemService _workItemService;

        public IndexModel(IWorkItemService workItemService)
        {
            _workItemService = workItemService;
        }

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Executor { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }

        public string DepartmentName { get; set; } = "Отдел №17";
        public string UserName { get; set; } = string.Empty;

        public List<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
        public List<string> Executors { get; set; } = new List<string>();

        [BindProperty]
        public string SelectedItemsOrder { get; set; } = string.Empty;

        public async Task OnGet()
        {
            // Пример чтения куки
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
            {
                Response.Redirect("/Login");
                return;
            }

            int divisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);
            UserName = HttpContext.Request.Cookies["userName"];

            // Установка дат по умолчанию, если не заданы
            if (!StartDate.HasValue)
                StartDate = new DateTime(2014, 1, 1);

            DateTime now = DateTime.Now;
            if (!EndDate.HasValue)
                EndDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);

            // Загружаем данные
            Executors = await _workItemService.GetExecutorsAsync(divisionId);
            WorkItems = await _workItemService.GetAllWorkItemsAsync(divisionId);
            DepartmentName = await _workItemService.GetDevAsync(divisionId);

            // Применяем фильтры к WorkItems
            ApplyFilters();
        }

        public async Task<IActionResult> OnGetFilterAsync(
            DateTime? startDate,
            DateTime? endDate,
            string? executor,
            string? searchQuery)
        {
            // AJAX-обработчик, вызываемый при изменении фильтров
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
                return new JsonResult(new { error = "Не найдены куки divisionId" });

            int divisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);

            StartDate = startDate ?? new DateTime(2014, 1, 1);
            EndDate = endDate ?? DateTime.Now;
            Executor = executor;
            SearchQuery = searchQuery;

            Executors = await _workItemService.GetExecutorsAsync(divisionId);
            WorkItems = await _workItemService.GetAllWorkItemsAsync(divisionId);
            DepartmentName = await _workItemService.GetDevAsync(divisionId);

            ApplyFilters();

            // Возвращаем partial (HTML-фрагмент) с таблицей
            return Partial("_WorkItemsTablePartial", this);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Генерация PDF
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
                return RedirectToPage("/Login");

            int divisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);

            Executors = await _workItemService.GetExecutorsAsync(divisionId);
            WorkItems = await _workItemService.GetAllWorkItemsAsync(divisionId);
            string dev = await _workItemService.GetDevAsync(divisionId);

            ApplyFilters();

            // Обработка выбранных позиций (SelectedItemsOrder)
            if (!string.IsNullOrEmpty(SelectedItemsOrder))
            {
                var selectedList = JsonSerializer.Deserialize<List<string>>(SelectedItemsOrder);
                if (selectedList != null && selectedList.Count > 0)
                {
                    // Фильтруем WorkItems, оставляем только те, DocumentNumber которых есть в selectedList
                    var filtered = WorkItems.Where(w => selectedList.Contains(w.DocumentNumber)).ToList();

                    // Сортируем в порядке, в котором DocumentNumber идут в selectedList
                    filtered = filtered.OrderBy(w => selectedList.IndexOf(w.DocumentNumber)).ToList();

                    WorkItems = filtered;
                }
            }

            // Генерация pdfBytes (ReportGenerator - ваш сервис/утилита, не показан в коде)
            var pdfBytes = ReportGenerator.GeneratePdf(WorkItems,
                          $"Сдаточный чек от {DateTime.Now.ToShortDateString()}", dev);

            return File(pdfBytes, "application/pdf", $"Чек_{DateTime.Now:yyyyMMdd}.pdf");
        }

        private void ApplyFilters()
        {
            // Применение выбранных фильтров
            var filtered = WorkItems.AsQueryable();

            if (!string.IsNullOrEmpty(Executor))
            {
                filtered = filtered.Where(x => x.Executor.Contains(Executor, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(SearchQuery))
            {
                filtered = filtered.Where(x =>
                    (x.DocumentName != null && x.DocumentName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (x.WorkName != null && x.WorkName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Executor != null && x.Executor.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Controller != null && x.Controller.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Approver != null && x.Approver.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)));
            }

            if (EndDate.HasValue)
            {
                filtered = filtered.Where(x =>
                    (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) <= EndDate);
            }

            WorkItems = filtered.ToList();
        }

        public IActionResult OnGetLogout()
        {
            // Логаут
            HttpContext.Response.Cookies.Delete("userName");
            HttpContext.Response.Cookies.Delete("divisionId");
            return RedirectToPage("Login");
        }
    }
}