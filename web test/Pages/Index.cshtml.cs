// Pages/Index.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using web_test.Services;

namespace web_test.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IWorkItemService _workItemService;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;

        public IndexModel(IWorkItemService workItemService, IMemoryCache cache, IConfiguration configuration)
        {
            _workItemService = workItemService;
            _cache = cache;
            _configuration = configuration;
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
        public List<SelectListItem> Executors { get; set; } = new List<SelectListItem>();
        public string Dev { get; set; } = " ";

        public async Task OnGet()
        {

            _cache.Remove("AllWorkItems");
            _cache.Remove("Executors");
            _cache.Remove("Dev");

            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
            {
                Response.Redirect("/Login");
                return;
            }

            var divisionIdString = HttpContext.Request.Cookies["divisionId"];
            if (!int.TryParse(divisionIdString, out int divisionId))
            {
                Response.Redirect("/Login");
                return;
            }

            UserName = HttpContext.Request.Cookies["userName"];

            if (!StartDate.HasValue)
                StartDate = new DateTime(2014, 1, 1);

            DateTime now = DateTime.Now;
            if (!EndDate.HasValue)
                EndDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);

            Executors = await _workItemService.GetExecutorsAsync(divisionId);
            WorkItems = await _workItemService.GetAllWorkItemsAsync(divisionId);
            Dev = await _workItemService.GetDevAsync(divisionId);

            DepartmentName = Dev;


            ApplyFilters();
        }
        // Этот метод будет вызываться только AJAX-ом,
        // и возвращать кусок HTML (partial) без layout.
        public async Task<IActionResult> OnGetFilterAsync(
            DateTime? startDate,
            DateTime? endDate,
            string? executor,
            string? searchQuery)
        {
            // Проверяем куки (как в основном OnGet)
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
                return new JsonResult(new { error = "Не найдены куки divisionId" });

            if (!int.TryParse(HttpContext.Request.Cookies["divisionId"], out int divisionId))
                return new JsonResult(new { error = "Некорректный divisionId в куках" });

            UserName = HttpContext.Request.Cookies["userName"];
            DepartmentName = $"Отдел №{divisionId}";

            // Задаём даты "по умолчанию", если не переданы
            if (!startDate.HasValue)
                startDate = new DateTime(2014, 1, 1);

            DateTime now = DateTime.Now;
            if (!endDate.HasValue)
                endDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);

            // Присваиваем в текущую модель, чтобы ApplyFilters() работал
            StartDate = startDate;
            EndDate = endDate;
            Executor = executor;
            SearchQuery = searchQuery;

            // Снова подтягиваем списки
            Executors = await _workItemService.GetExecutorsAsync(divisionId);
            WorkItems = await _workItemService.GetAllWorkItemsAsync(divisionId);

            ApplyFilters(); // Фильтруем

            // Возвращаем partial с таблицей.
            // В Razor Pages нет встроенного "return PartialView(...)",
            // поэтому используем PartialViewResult следующим образом:
            return new PartialViewResult
            {
                ViewName = "_WorkItemsTablePartial", // наш partial
                ViewData = this.ViewData,
                // Модель (IndexModel) чтобы внутри partial'а работали @Model.WorkItems
            };
        }

        private void ApplyFilters()
        {
            var filtered = WorkItems.AsQueryable();

            if (!string.IsNullOrEmpty(Executor))
                filtered = filtered.Where(x => x.Executor.Equals(Executor, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(SearchQuery))
            {
                var search = SearchQuery.Trim();
                filtered = filtered.Where(x =>
                    (x.DocumentName != null && x.DocumentName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (x.WorkName != null && x.WorkName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Executor != null && x.Executor.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Controller != null && x.Controller.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Approver != null && x.Approver.Contains(search, StringComparison.OrdinalIgnoreCase)));
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
            _cache.Remove("AllWorkItems");
            _cache.Remove("Executors");
            HttpContext.Response.Cookies.Delete("userName");
            HttpContext.Response.Cookies.Delete("divisionId");
            return RedirectToPage("Login");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Проверяем куки
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
                return RedirectToPage("/Login");

            if (!int.TryParse(HttpContext.Request.Cookies["divisionId"], out int divisionId))
                return RedirectToPage("/Login");

            // Загружаем из БД через сервис (или берем из кэша)
            Executors = await _workItemService.GetExecutorsAsync(divisionId);
            WorkItems = await _workItemService.GetAllWorkItemsAsync(divisionId);
            Dev = await _workItemService.GetDevAsync(divisionId);

            // Применяем те же фильтры, что и в OnGet / OnGetFilterAsync
            ApplyFilters();


            // Генерация PDF в памяти
            var pdfBytes = ReportGenerator.GeneratePdf(this.WorkItems, $"Сдаточный чек от {DateTime.Now.ToShortDateString()}", Dev);
            return File(pdfBytes, "application/pdf", $"Чек от {DateTime.Now.ToShortDateString()}.pdf");
        }
    }
}