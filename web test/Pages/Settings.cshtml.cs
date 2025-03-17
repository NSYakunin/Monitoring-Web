using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
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

        /// <summary>
        /// ������ ���� ������������� (smallName).
        /// </summary>
        public List<string> AllUsers { get; set; } = new();

        /// <summary>
        /// ����: ���������� �� ���������� �������������.
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public bool ShowInactive { get; set; }

        /// <summary>
        /// ��� ���������� ������������.
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public string? SelectedUserName { get; set; }

        /// <summary>
        /// ������� ��������� �����������.
        /// </summary>
        public PrivacySettingsDto CurrentPrivacySettings { get; set; } = new();

        /// <summary>
        /// ���������� ���� �������������.
        /// </summary>
        public List<DivisionDto> Subdivisions { get; set; } = new();

        /// <summary>
        /// ������ idDivision, ������� ��������� ������������.
        /// </summary>
        public List<int> UserSelectedDivisionIds { get; set; } = new();

        /// <summary>
        /// ������� ������ (�� ������� Users.Password).
        /// </summary>
        public string? CurrentPasswordForSelectedUser { get; set; }

        /// <summary>
        /// ����������, �������� �� ������������ �������� (Isvalid=1).
        /// </summary>
        public bool IsUserValid { get; set; }

        /// <summary>
        /// ������ ����� ���������� ������������ (idDivision �� Users).
        /// �����, ����� ������ ���� ��� ������ �� ������ ���������.
        /// </summary>
        public int? HomeDivisionIdForSelectedUser { get; set; }

        public void OnGet()
        {
            // �������� ����, ������ � �.�. (�� ������)
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

            // 1. ������ ������������� � ���� ��������, ���� ����������
            if (!ShowInactive)
            {
                AllUsers = _loginService.GetAllUsersAsync().Result;
            }
            else
            {
                AllUsers = _loginService.GetAllInactiveUsersAsync().Result;
            }

            // 2. ��� �������������
            Subdivisions = _userSettingsService.GetAllDivisionsAsync().Result;

            // 3. ���� ������ ������������, ������ ��� ���������
            if (!string.IsNullOrEmpty(SelectedUserName))
            {
                int? selUserId = _loginService.GetUserIdByNameAsync(SelectedUserName).Result;
                if (selUserId != null)
                {
                    CurrentPrivacySettings = _userSettingsService.GetPrivacySettingsAsync(selUserId.Value).Result;
                    UserSelectedDivisionIds = _userSettingsService.GetUserAllowedDivisionsAsync(selUserId.Value).Result;
                    CurrentPasswordForSelectedUser = _userSettingsService.GetUserCurrentPasswordAsync(selUserId.Value).Result;
                    IsUserValid = _userSettingsService.IsUserValidAsync(selUserId.Value).Result;

                    // ������ ����� (idDivision) �� ������� Users
                    HomeDivisionIdForSelectedUser = Convert.ToInt32(_userSettingsService.GetUserHomeDivisionAsync(selUserId.Value).Result);
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

                // ����� �����: ������� isActive (�������/�� �������)
                bool isActive = ((JsonElement)data["isActive"]).GetBoolean();

                int? userId = _loginService.GetUserIdByNameAsync(userName).Result;
                if (userId == null)
                    return new JsonResult(new { success = false, message = "������������ �� ������" });

                var dto = new PrivacySettingsDto
                {
                    CanCloseWork = canClose,
                    CanSendCloseRequest = canSend,
                    CanAccessSettings = canAccess
                };
                // ������ �������� ����� ����� (� ���������� isActive)
                _userSettingsService.SavePrivacySettingsAsync(userId.Value, dto, isActive).Wait();

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

        // POST: ����� ������ ��� ���������� ������������
        public IActionResult OnPostChangeUserPassword()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "���������� JSON" });

                string userName = data["userName"]?.ToString();
                string newPassword = data["newPassword"]?.ToString();

                if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(newPassword))
                {
                    return new JsonResult(new { success = false, message = "�� ������ userName ��� newPassword." });
                }

                int? userId = _loginService.GetUserIdByNameAsync(userName).Result;
                if (userId == null)
                    return new JsonResult(new { success = false, message = "������������ �� ������" });

                // �������� ����� ��� ����� ������ � Users
                _userSettingsService.ChangeUserPasswordAsync(userId.Value, newPassword).Wait();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // POST: ����������� ������ ������������
        public IActionResult OnPostRegisterUser()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "���������� JSON" });

                string fio = data["fullName"]?.ToString();     // = Name
                string smallName = data["smallName"]?.ToString();
                string password = data["password"]?.ToString();
                bool canClose = Convert.ToBoolean(data["canCloseWork"]);
                bool canSend = Convert.ToBoolean(data["canSendCloseRequest"]);
                bool canSettings = Convert.ToBoolean(data["canAccessSettings"]);

                // �������������
                int? idDivision = null;
                if (data.ContainsKey("idDivision") && data["idDivision"] != null)
                {
                    // ���� � JSON ���� null, �� ������� �� �����
                    if (int.TryParse(data["idDivision"].ToString(), out var divId))
                    {
                        idDivision = divId;
                    }
                }

                if (string.IsNullOrWhiteSpace(fio) || string.IsNullOrWhiteSpace(smallName) || string.IsNullOrWhiteSpace(password))
                {
                    return new JsonResult(new { success = false, message = "�� ��� ������������ ���� ��������� (���, login, ������)." });
                }

                // ��������, ����� ����� ������������ ��� ����
                int? existingUserId = _loginService.GetUserIdByNameAsync(smallName).Result;
                if (existingUserId != null)
                {
                    return new JsonResult(new { success = false, message = "������������ � ����� ������� ��� ����������." });
                }

                // ������ ������ � Users (idTypeUser=2, Isvalid=1)
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