using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Numerics;

namespace web_test.Pages
{
    public class IndexModel : PageModel
    {
        public string Message { get; private set; } = "";
        public void OnGet(string name = "Vasia", int age = 123)
        {
            Message = $"Name: {name} и ему {age} лет";
        }
    }
}
