using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
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

        /// <summary>
        /// Список всех пользователей (smallName).
        /// </summary>
        public List<string> AllUsers { get; set; } = new();

        /// <summary>
        /// Флаг: Показывать ли неактивных пользователей.
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public bool ShowInactive { get; set; }

        /// <summary>
        /// Имя выбранного пользователя.
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public string? SelectedUserName { get; set; }

        /// <summary>
        /// Текущие настройки приватности.
        /// </summary>
        public PrivacySettingsDto CurrentPrivacySettings { get; set; } = new();

        /// <summary>
        /// Справочник всех подразделений.
        /// </summary>
        public List<DivisionDto> Subdivisions { get; set; } = new();

        /// <summary>
        /// Список idDivision, которые разрешены пользователю.
        /// </summary>
        public List<int> UserSelectedDivisionIds { get; set; } = new();

        /// <summary>
        /// Текущий пароль (из таблицы Users.Password).
        /// </summary>
        public string? CurrentPasswordForSelectedUser { get; set; }

        /// <summary>
        /// Показывает, является ли пользователь активным (Isvalid=1).
        /// </summary>
        public bool IsUserValid { get; set; }

        /// <summary>
        /// Родной отдел выбранного пользователя (idDivision из Users).
        /// Нужно, чтобы нельзя было его убрать из списка чекбоксов.
        /// </summary>
        public int? HomeDivisionIdForSelectedUser { get; set; }

        public void OnGet()
        {
            // Проверка куки, доступ и т.д. (не меняем)
            if (!HttpContext.Request.Cookies.ContainsKey("userName"))
            {
                Response.Redirect("/Login");
                return;
            }

            string userName = HttpContext.Request.Cookies["userName"];
            int? userIdP = _loginService.GetUserIdByNameAsync(userName).Result;
            if (userIdP == null)
            {
                Response.Redirect("/Login");
                return;
            }

            bool canAccess = _userSettingsService.HasAccessToSettingsAsync(userIdP.Value).Result;
            if (!canAccess)
            {
                Response.Redirect("/Index");
                return;
            }

            // 1. Список пользователей — либо активных, либо неактивных
            if (!ShowInactive)
            {
                AllUsers = _loginService.GetAllUsersAsync().Result;
            }
            else
            {
                AllUsers = _loginService.GetAllInactiveUsersAsync().Result;
            }

            // 2. Все подразделения
            Subdivisions = _userSettingsService.GetAllDivisionsAsync().Result;

            // 3. Если выбран пользователь, грузим его настройки
            if (!string.IsNullOrEmpty(SelectedUserName))
            {
                int? selUserId = _loginService.GetUserIdByNameAsync(SelectedUserName).Result;
                if (selUserId != null)
                {
                    CurrentPrivacySettings = _userSettingsService.GetPrivacySettingsAsync(selUserId.Value).Result;
                    UserSelectedDivisionIds = _userSettingsService.GetUserAllowedDivisionsAsync(selUserId.Value).Result;
                    CurrentPasswordForSelectedUser = _userSettingsService.GetUserCurrentPasswordAsync(selUserId.Value).Result;
                    IsUserValid = _userSettingsService.IsUserValidAsync(selUserId.Value).Result;

                    // Родной отдел (idDivision) из таблицы Users
                    HomeDivisionIdForSelectedUser = Convert.ToInt32(_userSettingsService.GetUserHomeDivisionAsync(selUserId.Value).Result);
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

                // Новая часть: Признак isActive (валиден/не валиден)
                bool isActive = ((JsonElement)data["isActive"]).GetBoolean();

                int? userId = _loginService.GetUserIdByNameAsync(userName).Result;
                if (userId == null)
                    return new JsonResult(new { success = false, message = "Пользователь не найден" });

                var dto = new PrivacySettingsDto
                {
                    CanCloseWork = canClose,
                    CanSendCloseRequest = canSend,
                    CanAccessSettings = canAccess
                };
                // Теперь вызываем новый метод (с параметром isActive)
                _userSettingsService.SavePrivacySettingsAsync(userId.Value, dto, isActive).Wait();

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
                    return new JsonResult(new { success = false, message = "Не все обязательные поля заполнены (ФИО, login, пароль)." });
                }

                // Проверим, вдруг такой пользователь уже есть
                int? existingUserId = _loginService.GetUserIdByNameAsync(smallName).Result;
                if (existingUserId != null)
                {
                    return new JsonResult(new { success = false, message = "Пользователь с таким логином уже существует." });
                }

                // Создаём запись в Users (idTypeUser=2, Isvalid=1)
                int newUserId = _userSettingsService.RegisterUserInDbAsync(
                    fio,
                    smallName,
                    password,
                    idDivision,
                    canClose,
                    canSend,
                    canSettings
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