using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Application.Services; // Для PrivacySettingsDto, DivisionDto
using System.Text.Json;

namespace Monitoring.UI.Pages
{
    public class SettingsModel : PageModel
    {
        private readonly IUserSettingsService _userSettingsService;
        private readonly ILoginService _loginService;

        public SettingsModel(IUserSettingsService userSettingsService, ILoginService loginService)
        {
            _userSettingsService = userSettingsService;
            _loginService = loginService;
        }

        // Список всех пользователей (smallName, login или как у вас заведено).
        // Предположим, что _loginService.GetAllUsersAsync() возвращает список логинов или smallName.
        public List<string> AllUsers { get; set; } = new();

        // Имя выбранного пользователя (smallName или login)
        [BindProperty(SupportsGet = true)]
        public string? SelectedUserName { get; set; }

        // Текущие настройки приватности
        public PrivacySettingsDto CurrentPrivacySettings { get; set; } = new();

        // Справочник подразделений
        public List<DivisionDto> Subdivisions { get; set; } = new();

        // Какие подразделения выбраны у пользователя
        public List<int> UserSelectedDivisionIds { get; set; } = new();

        public void OnGet()
        {
            // Предположим, берём userName (логин) из куки
            if (!HttpContext.Request.Cookies.ContainsKey("userName"))
            {
                Response.Redirect("/Login");
                return;
            }

            string userName = HttpContext.Request.Cookies["userName"];
            // Нужен userId
            int? userIdP = _loginService.GetUserIdByNameAsync(userName).Result;
            if (userIdP == null)
            {
                Response.Redirect("/Login");
                return;
            }

            // Проверяем право на доступ к этой странице
            bool canAccess = _userSettingsService.HasAccessToSettingsAsync(userIdP.Value).Result;
            if (!canAccess)
            {
                // Редирект на главную или 403
                Response.Redirect("/Index");
                return;
            }

            // 1. Список всех пользователей
            AllUsers = _loginService.GetAllUsersAsync().Result;

            // 2. Список всех подразделений
            Subdivisions = _userSettingsService.GetAllDivisionsAsync().Result;

            // 3. Если кто-то выбран
            if (!string.IsNullOrEmpty(SelectedUserName))
            {
                // Находим его idUser
                int? userId = _loginService.GetUserIdByNameAsync(SelectedUserName).Result;
                if (userId != null)
                {
                    // Грузим настройки приватности
                    CurrentPrivacySettings = _userSettingsService.GetPrivacySettingsAsync(userId.Value).Result;

                    // Грузим выбранные подразделения
                    UserSelectedDivisionIds = _userSettingsService.GetUserAllowedDivisionsAsync(userId.Value).Result;
                }
            }
        }

        // POST: Сохранение настроек приватности
        public IActionResult OnPostSavePrivacySettings()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "Невалидный JSON" });

                string userName = data["userName"].ToString();
                bool canClose = ((JsonElement)data["canCloseWork"]).GetBoolean();
                bool canSend = ((JsonElement)data["canSendCloseRequest"]).GetBoolean();
                bool canAccess = ((JsonElement)data["canAccessSettings"]).GetBoolean();

                int? userId = _loginService.GetUserIdByNameAsync(userName).Result;
                if (userId == null)
                    return new JsonResult(new { success = false, message = "Пользователь не найден" });

                var dto = new PrivacySettingsDto
                {
                    CanCloseWork = canClose,
                    CanSendCloseRequest = canSend,
                    CanAccessSettings = canAccess
                };
                _userSettingsService.SavePrivacySettingsAsync(userId.Value, dto).Wait();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // POST: Сохранение списка подразделений
        public IActionResult OnPostSaveSubdivisions()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "Невалидный JSON" });

                string userName = data["userName"].ToString();
                var subdivisionsElement = data["subdivisions"] as JsonElement?;

                int? userId = _loginService.GetUserIdByNameAsync(userName).Result;
                if (userId == null)
                    return new JsonResult(new { success = false, message = "Пользователь не найден" });

                var subIds = new List<int>();
                if (subdivisionsElement.HasValue &&
                    subdivisionsElement.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in subdivisionsElement.Value.EnumerateArray())
                    {
                        subIds.Add(el.GetInt32());
                    }
                }

                _userSettingsService.SaveUserAllowedDivisionsAsync(userId.Value, subIds).Wait();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // POST: Смена пароля для выбранного пользователя
        public IActionResult OnPostChangeUserPassword()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "Невалидный JSON" });

                string userName = data["userName"]?.ToString();
                string newPassword = data["newPassword"]?.ToString();

                if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(newPassword))
                {
                    return new JsonResult(new { success = false, message = "Не заданы userName или newPassword." });
                }

                int? userId = _loginService.GetUserIdByNameAsync(userName).Result;
                if (userId == null)
                    return new JsonResult(new { success = false, message = "Пользователь не найден" });

                // Вызываем метод для смены пароля в Users
                _userSettingsService.ChangeUserPasswordAsync(userId.Value, newPassword).Wait();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // POST: Регистрация нового пользователя
        public IActionResult OnPostRegisterUser()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "Невалидный JSON" });

                string fio = data["fullName"]?.ToString();     // = Name
                string smallName = data["smallName"]?.ToString();
                //string login = data["login"]?.ToString();
                string password = data["password"]?.ToString();
                bool canClose = Convert.ToBoolean(data["canCloseWork"]);
                bool canSend = Convert.ToBoolean(data["canSendCloseRequest"]);
                bool canSettings = Convert.ToBoolean(data["canAccessSettings"]);

                // Подразделение
                int? idDivision = null;
                if (data.ContainsKey("idDivision") && data["idDivision"] != null)
                {
                    // Если в JSON было null, то парсить не будем
                    if (int.TryParse(data["idDivision"].ToString(), out var divId))
                    {
                        idDivision = divId;
                    }
                }

                if (string.IsNullOrWhiteSpace(fio) || string.IsNullOrWhiteSpace(smallName) || string.IsNullOrWhiteSpace(password))
                {
                    return new JsonResult(new { success = false, message = "Не все обязательные поля заполнены (ФИО, логин, пароль)." });
                }

                // Проверим, вдруг такой пользователь уже есть
                int? existingUserId = _loginService.GetUserIdByNameAsync(smallName).Result;
                if (existingUserId != null)
                {
                    // Или проверять по FIO/smallName — зависит от ваших правил
                    return new JsonResult(new { success = false, message = "Пользователь с таким логином уже существует." });
                }

                // Создаём запись в Users (idTypeUser=2, Isvalid=1)
                int newUserId = _userSettingsService.RegisterUserInDbAsync(
                    fio, smallName, password, idDivision, canClose, canSend, canSettings
                ).Result;

                return new JsonResult(new { success = true, newUserId = newUserId });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }
}