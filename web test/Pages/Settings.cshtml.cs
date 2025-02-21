using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace Monitoring.UI.Pages
{
    public class SettingsModel : PageModel
    {
        // Модель пользователя (пример)
        public class UserDto
        {
            public int UserId { get; set; }
            public string DisplayName { get; set; } = "";
        }

        // Модель подразделения
        public class SubdivisionDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        // Настройки приватности
        public class PrivacySettingsDto
        {
            public bool CanCloseWork { get; set; }
            public bool CanSendCloseRequest { get; set; }
            public bool CanAccessSettings { get; set; }
        }

        // Демонстрационные свойства:
        public List<UserDto> AllUsers { get; set; } = new();
        public int SelectedUserId { get; set; }
        public PrivacySettingsDto CurrentPrivacySettings { get; set; } = new();

        // Список всех подразделений (для примера)
        public List<SubdivisionDto> Subdivisions { get; set; } = new();
        // Список ID подразделений, которые выбраны у пользователя
        public List<int> UserSelectedSubdivisions { get; set; } = new();

        // Имитируем какой-то сервис
        // private readonly IUserSettingsService _userSettingsService;
        // public SettingsModel(IUserSettingsService userSettingsService)
        // {
        //     _userSettingsService = userSettingsService;
        // }

        public void OnGet(int userId)
        {
            // 1) Загрузим всех пользователей
            AllUsers = LoadAllUsers();

            // 2) Загрузим список подразделений
            Subdivisions = LoadAllSubdivisions();

            // 3) Если есть параметр userId, считаем, что выбрали конкретного пользователя
            SelectedUserId = userId;

            if (SelectedUserId != 0)
            {
                // 4) Загрузить настройки приватности
                CurrentPrivacySettings = LoadUserPrivacySettings(SelectedUserId);

                // 5) Загрузить выбранные подразделения
                UserSelectedSubdivisions = LoadUserSubdivisions(SelectedUserId);
            }
        }

        // Пример POST-обработчика: Сохранение PrivacySettings
        public IActionResult OnPostSavePrivacySettings()
        {
            try
            {
                // Считываем JSON из тела запроса
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "Невалидный JSON" });

                int userId = Convert.ToInt32(data["userId"]);
                bool canCloseWork = Convert.ToBoolean(data["canCloseWork"]);
                bool canSendClose = Convert.ToBoolean(data["canSendCloseRequest"]);
                bool canAccess = Convert.ToBoolean(data["canAccessSettings"]);

                // Вызываем условный сервис
                SaveUserPrivacySettings(userId, canCloseWork, canSendClose, canAccess);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // Пример POST-обработчика: Сохранение &laquo;Выбора подразделений&raquo;
        public IActionResult OnPostSaveSubdivisions()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "Невалидный JSON" });

                int userId = Convert.ToInt32(data["userId"]);
                var subdivisionsArray = data["subdivisions"] as JsonElement?;
                List<int> subIds = new List<int>();

                if (subdivisionsArray.HasValue && subdivisionsArray.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in subdivisionsArray.Value.EnumerateArray())
                    {
                        subIds.Add(elem.GetInt32());
                    }
                }

                // Вызываем условный сервис
                SaveUserSubdivisions(userId, subIds);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // Регистрация нового пользователя
        public IActionResult OnPostRegisterUser()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "Невалидный JSON" });

                string fio = data["fullName"].ToString();
                string login = data["login"].ToString();
                string password = data["password"].ToString();
                bool canClose = Convert.ToBoolean(data["canCloseWork"]);
                bool canSend = Convert.ToBoolean(data["canSendCloseRequest"]);
                bool canSettings = Convert.ToBoolean(data["canAccessSettings"]);

                // Пример: вызываем сервис
                // _userSettingsService.CreateUser(fio, login, password, canClose, canSend, canSettings);
                // Здесь — просто макет:
                if (string.IsNullOrWhiteSpace(login))
                    return new JsonResult(new { success = false, message = "Логин пуст." });

                // якобы добавили в БД...
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // -------------------------------------------------
        // Ниже "мок" методы, которые имитируют работу с БД
        // -------------------------------------------------

        private List<UserDto> LoadAllUsers()
        {
            // Имитируем загрузку списка пользователей из базы
            return new List<UserDto>
            {
                new UserDto { UserId = 1, DisplayName = "Иванов Иван" },
                new UserDto { UserId = 2, DisplayName = "Петров Петр" },
                new UserDto { UserId = 3, DisplayName = "Сидоров Максим" },
                new UserDto { UserId = 4, DisplayName = "Александрова Анна" }
            };
        }

        private PrivacySettingsDto LoadUserPrivacySettings(int userId)
        {
            // Имитируем, что берем из базы настройки
            // В реальности: SELECT ... FROM UserPrivacySettings WHERE userId = ...
            // Для наглядности возвращаем какие-то тестовые данные
            return new PrivacySettingsDto
            {
                CanCloseWork = (userId % 2 == 1),
                CanSendCloseRequest = true,
                CanAccessSettings = (userId == 1) // Допустим, только у userId=1 включено
            };
        }

        private void SaveUserPrivacySettings(int userId, bool canClose, bool canSend, bool canSettings)
        {
            // Мок-метод: UPDATE UserPrivacySettings SET ...
            // Допустим, успешно сохранили
        }

        private List<SubdivisionDto> LoadAllSubdivisions()
        {
            // Имитируем список подразделений
            return new List<SubdivisionDto>
            {
                new SubdivisionDto{ Id=10, Name="Отдел 10"},
                new SubdivisionDto{ Id=11, Name="Отдел 11"},
                new SubdivisionDto{ Id=12, Name="Отдел 12"},
                new SubdivisionDto{ Id=13, Name="Отдел 13"},
            };
        }

        private List<int> LoadUserSubdivisions(int userId)
        {
            // Имитируем, что пользователь userId может смотреть некоторые подразделения
            // В реальном проекте — SELECT ... FROM UserAllowedSubdivisions WHERE userId=...
            if (userId == 1) return new List<int> { 10, 12 };
            if (userId == 2) return new List<int> { 11 };
            return new List<int>();
        }

        private void SaveUserSubdivisions(int userId, List<int> subIds)
        {
            // Мок: UPDATE UserAllowedSubdivisions ...
        }
    }
}