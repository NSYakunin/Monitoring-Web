using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Application.Services; // ��� PrivacySettingsDto, DivisionDto
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

        // ������ ���� ������������� (smallName)
        public List<string> AllUsers { get; set; } = new();

        // ��� ���������� ������������ (smallName)
        [BindProperty(SupportsGet = true)]
        public string? SelectedUserName { get; set; }

        // ������� ��������� �����������
        public PrivacySettingsDto CurrentPrivacySettings { get; set; } = new();

        // ���������� �������������
        public List<DivisionDto> Subdivisions { get; set; } = new();

        // ����� ������������� ������� � ������������
        public List<int> UserSelectedDivisionIds { get; set; } = new();

        public void OnGet()
        {
            // �����������, �� ����� userName �� ����
            if (!HttpContext.Request.Cookies.ContainsKey("userName"))
            {
                Response.Redirect("/Login");
                return;
            }

            string userName = HttpContext.Request.Cookies["userName"];
            // ����� userId
            int? userIdP = _loginService.GetUserIdByNameAsync(userName).Result;
            if (userIdP == null)
            {
                Response.Redirect("/Login");
                return;
            }

            // ��������� �����
            bool canAccess = _userSettingsService.HasAccessToSettingsAsync(userIdP.Value).Result;
            if (!canAccess)
            {
                // ����� ����������� �� �������, ���� ���������� 403 (Forbidden)
                // ������:
                Response.Redirect("/Index");  // ���
                                              // return Forbid(); 
                return;
            }

            // 1. ������ ���� �������������
            AllUsers = _loginService.GetAllUsersAsync().Result;

            // 2. ������ ���� �������������
            Subdivisions = _userSettingsService.GetAllDivisionsAsync().Result;

            // 3. ���� ���-�� ������
            if (!string.IsNullOrEmpty(SelectedUserName))
            {
                // ������� ��� idUser
                int? userId = _loginService.GetUserIdByNameAsync(SelectedUserName).Result;
                if (userId != null)
                {
                    // ������ ��������� �����������
                    CurrentPrivacySettings = _userSettingsService.GetPrivacySettingsAsync(userId.Value).Result;

                    // ������ ��������� �������������
                    UserSelectedDivisionIds = _userSettingsService.GetUserAllowedDivisionsAsync(userId.Value).Result;
                }
            }
        }

        // POST: ���������� �������� �����������
        public IActionResult OnPostSavePrivacySettings()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "���������� JSON" });

                string userName = data["userName"].ToString();
                bool canClose = ((JsonElement)data["canCloseWork"]).GetBoolean();
                bool canSend = ((JsonElement)data["canSendCloseRequest"]).GetBoolean();
                bool canAccess = ((JsonElement)data["canAccessSettings"]).GetBoolean();

                int? userId = _loginService.GetUserIdByNameAsync(userName).Result;
                if (userId == null)
                    return new JsonResult(new { success = false, message = "������������ �� ������" });

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

        // POST: ���������� ������ �������������
        public IActionResult OnPostSaveSubdivisions()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "���������� JSON" });

                string userName = data["userName"].ToString();
                var subdivisionsElement = data["subdivisions"] as JsonElement?;

                int? userId = _loginService.GetUserIdByNameAsync(userName).Result;
                if (userId == null)
                    return new JsonResult(new { success = false, message = "������������ �� ������" });

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

        // POST: ����������� ������ ������������ (������)
        public IActionResult OnPostRegisterUser()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "���������� JSON" });

                string fio = data["fullName"]?.ToString();
                string login = data["login"]?.ToString();
                string password = data["password"]?.ToString();
                bool canClose = Convert.ToBoolean(data["canCloseWork"]);
                bool canSend = Convert.ToBoolean(data["canSendCloseRequest"]);
                bool canSettings = Convert.ToBoolean(data["canAccessSettings"]);

                if (string.IsNullOrWhiteSpace(login))
                    return new JsonResult(new { success = false, message = "����� ����." });

                // ����� ����� ���� ����� ������������ ������, ������� ������� 
                // ����� ������ � [Users], � ����� ������� � [UserPrivacy].
                // ��� ������� � ������ "�����".
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }
}