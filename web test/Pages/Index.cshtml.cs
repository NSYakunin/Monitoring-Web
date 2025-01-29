using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Numerics;

namespace web_test.Pages
{
    public class IndexModel : PageModel
    {
        public string Message { get; private set; } = "";
        public void OnGet(Person person)
        {
            Message = $"Person: {person.Name} � ��� {person.Age} ���";
        }
    }

    public record class Person(string Name, int Age);
}
