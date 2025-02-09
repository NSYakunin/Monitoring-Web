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

        public async Task OnGet()
        {
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
            DepartmentName = $"Отдел №{divisionId}";

            if (!StartDate.HasValue)
                StartDate = new DateTime(2014, 1, 1);

            DateTime now = DateTime.Now;
            if (!EndDate.HasValue)
                EndDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);

            Executors = await _workItemService.GetExecutorsAsync(divisionId);
            WorkItems = await _workItemService.GetAllWorkItemsAsync(divisionId);

            ApplyFilters();
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
            HttpContext.Response.Cookies.Delete("userName");
            HttpContext.Response.Cookies.Delete("divisionId");
            return RedirectToPage("Login");
        }
    }
}