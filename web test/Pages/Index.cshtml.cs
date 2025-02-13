// Monitoring.UI/Pages/Index.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Monitoring.Application.Interfaces;
using Monitoring.Application.Services; // если ReportGenerator лежит в .Application
using Monitoring.Domain.Entities;

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

            if (!StartDate.HasValue)
                StartDate = new DateTime(2014, 1, 1);

            DateTime now = DateTime.Now;
            if (!EndDate.HasValue)
                EndDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);

            // Загружаем данные
            Executors = await _workItemService.GetExecutorsAsync(divisionId);
            WorkItems = await _workItemService.GetAllWorkItemsAsync(divisionId);
            DepartmentName = await _workItemService.GetDevAsync(divisionId);

            // Применяем фильтры (можно было бы вынести в Application-слой).
            ApplyFilters();
        }

        public async Task<IActionResult> OnGetFilterAsync(
            DateTime? startDate,
            DateTime? endDate,
            string? executor,
            string? searchQuery)
        {
            // Пример AJAX-обработчика
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

            // Возвращаем partial
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

            // Теперь учитываем SelectedItemsOrder
            if (!string.IsNullOrEmpty(SelectedItemsOrder))
            {
                // Парсим JSON
                // Допустим, это массив строк DocumentNumber: ["123/1","999/2",...]
                var selectedList = System.Text.Json.JsonSerializer.Deserialize<List<string>>(SelectedItemsOrder);

                if (selectedList != null && selectedList.Count > 0)
                {
                    // 1. Фильтруем WorkItems => только где DocumentNumber есть в selectedList
                    var filtered = WorkItems.Where(w => selectedList.Contains(w.DocumentNumber)).ToList();

                    // 2. Сортируем по порядку, в котором они идут в selectedList
                    //    Можно сделать так:
                    filtered = filtered.OrderBy(w => selectedList.IndexOf(w.DocumentNumber)).ToList();

                    // 3. Перезаписываем WorkItems
                    WorkItems = filtered;
                }
            }

            var pdfBytes = ReportGenerator.GeneratePdf(WorkItems, $"Сдаточный чек от {DateTime.Now.ToShortDateString()}", dev);
            return File(pdfBytes, "application/pdf", $"Чек_{DateTime.Now:yyyyMMdd}.pdf");
        }

        private void ApplyFilters()
        {
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