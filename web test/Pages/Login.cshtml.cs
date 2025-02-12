// Monitoring.UI/Pages/Login.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitoring.Application.Interfaces;

namespace Monitoring.UI.Pages
{
    public class LoginModel : PageModel
    {
        private readonly ILoginService _loginService;

        public LoginModel(ILoginService loginService)
        {
            _loginService = loginService;
        }

        // ������ ���� �������������
        public List<string> AllUsers { get; set; } = new List<string>();

        // ����, ���� ����������� �����
        [BindProperty]
        public string SelectedUser { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        // ���� ������ �����������
        public string ErrorMessage { get; set; } = string.Empty;

        public async Task OnGet()
        {
            AllUsers = await _loginService.GetAllUsersAsync();
        }

        public async Task<IActionResult> OnPost()
        {
            // ����� ������ ������ (����� �� ������� ��� ������������ ��������)
            AllUsers = await _loginService.GetAllUsersAsync();

            if (string.IsNullOrEmpty(SelectedUser))
            {
                ErrorMessage = "����������, �������� ������������.";
                return Page();
            }

            if (string.IsNullOrEmpty(Password))
            {
                ErrorMessage = "������� ������.";
                return Page();
            }

            // ���������
            var (divisionId, isValid) = await _loginService.CheckUserCredentialsAsync(SelectedUser, Password);
            if (!isValid || divisionId == null)
            {
                ErrorMessage = "�������� ��� ������������ ��� ������.";
                return Page();
            }

            // ���������� � ����
            HttpContext.Response.Cookies.Append("userName", SelectedUser);
            HttpContext.Response.Cookies.Append("divisionId", divisionId.Value.ToString());

            // ��������
            return RedirectToPage("Index");
        }

        // AJAX-�����:
        public async Task<IActionResult> OnGetFilterUsers(string query)
        {
            var filtered = await _loginService.FilterUsersAsync(query);
            return new JsonResult(filtered);
        }
    }
}