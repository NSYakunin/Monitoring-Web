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

        // Список всех пользователей
        public List<string> AllUsers { get; set; } = new List<string>();

        // Поля, куда привязываем форму
        [BindProperty]
        public string SelectedUser { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        // Если ошибка авторизации
        public string ErrorMessage { get; set; } = string.Empty;

        public async Task OnGet()
        {
            AllUsers = await _loginService.GetAllUsersAsync();
        }

        public async Task<IActionResult> OnPost()
        {
            // Снова грузим список (чтобы не пропали при перезагрузке страницы)
            AllUsers = await _loginService.GetAllUsersAsync();

            if (string.IsNullOrEmpty(SelectedUser))
            {
                ErrorMessage = "Пожалуйста, выберите пользователя.";
                return Page();
            }

            if (string.IsNullOrEmpty(Password))
            {
                ErrorMessage = "Введите пароль.";
                return Page();
            }

            // Проверяем
            var (divisionId, isValid) = await _loginService.CheckUserCredentialsAsync(SelectedUser, Password);
            if (!isValid || divisionId == null)
            {
                ErrorMessage = "Неверное имя пользователя или пароль.";
                return Page();
            }

            // Записываем в куки
            HttpContext.Response.Cookies.Append("userName", SelectedUser);
            HttpContext.Response.Cookies.Append("divisionId", divisionId.Value.ToString());

            // Редирект
            return RedirectToPage("Index");
        }

        // AJAX-метод:
        public async Task<IActionResult> OnGetFilterUsers(string query)
        {
            var filtered = await _loginService.FilterUsersAsync(query);
            return new JsonResult(filtered);
        }
    }
}