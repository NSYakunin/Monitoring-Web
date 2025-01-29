using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Numerics;

namespace web_test.Pages
{
    [IgnoreAntiforgeryToken]
    public class IndexModel : PageModel
    {
        [BindProperty]
        public Person? Person { get; set; }

    }

    public record Person(string Name, int Age, bool Orientacia);
}
