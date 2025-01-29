using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Numerics;

namespace web_test.Pages
{
    public class IndexModel : PageModel
    {
        // ѕример: если хотите передавать название подразделени€ в View
        public string DepartmentName { get; set; } = "ќтдел разработки";

        // ≈сли нужно обрабатывать запросы (GET/POST), переопредел€ем методы:
        public void OnGet()
        {
            // Ћогика, котора€ выполн€етс€ при загрузке страницы
            // Ќапример, получение данных, заполнение ViewModel и т.д.
        }

        // public IActionResult OnPost...()
        // ≈сли нужна обработка форм, можно создать соответствующие методы POST
    }
}
