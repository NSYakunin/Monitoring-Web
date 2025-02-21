using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace Monitoring.UI.Pages
{
    public class SettingsModel : PageModel
    {
        // ������ ������������ (������)
        public class UserDto
        {
            public int UserId { get; set; }
            public string DisplayName { get; set; } = "";
        }

        // ������ �������������
        public class SubdivisionDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        // ��������� �����������
        public class PrivacySettingsDto
        {
            public bool CanCloseWork { get; set; }
            public bool CanSendCloseRequest { get; set; }
            public bool CanAccessSettings { get; set; }
        }

        // ���������������� ��������:
        public List<UserDto> AllUsers { get; set; } = new();
        public int SelectedUserId { get; set; }
        public PrivacySettingsDto CurrentPrivacySettings { get; set; } = new();

        // ������ ���� ������������� (��� �������)
        public List<SubdivisionDto> Subdivisions { get; set; } = new();
        // ������ ID �������������, ������� ������� � ������������
        public List<int> UserSelectedSubdivisions { get; set; } = new();

        // ��������� �����-�� ������
        // private readonly IUserSettingsService _userSettingsService;
        // public SettingsModel(IUserSettingsService userSettingsService)
        // {
        //     _userSettingsService = userSettingsService;
        // }

        public void OnGet(int userId)
        {
            // 1) �������� ���� �������������
            AllUsers = LoadAllUsers();

            // 2) �������� ������ �������������
            Subdivisions = LoadAllSubdivisions();

            // 3) ���� ���� �������� userId, �������, ��� ������� ����������� ������������
            SelectedUserId = userId;

            if (SelectedUserId != 0)
            {
                // 4) ��������� ��������� �����������
                CurrentPrivacySettings = LoadUserPrivacySettings(SelectedUserId);

                // 5) ��������� ��������� �������������
                UserSelectedSubdivisions = LoadUserSubdivisions(SelectedUserId);
            }
        }

        // ������ POST-�����������: ���������� PrivacySettings
        public IActionResult OnPostSavePrivacySettings()
        {
            try
            {
                // ��������� JSON �� ���� �������
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "���������� JSON" });

                int userId = Convert.ToInt32(data["userId"]);
                bool canCloseWork = Convert.ToBoolean(data["canCloseWork"]);
                bool canSendClose = Convert.ToBoolean(data["canSendCloseRequest"]);
                bool canAccess = Convert.ToBoolean(data["canAccessSettings"]);

                // �������� �������� ������
                SaveUserPrivacySettings(userId, canCloseWork, canSendClose, canAccess);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // ������ POST-�����������: ���������� &laquo;������ �������������&raquo;
        public IActionResult OnPostSaveSubdivisions()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "���������� JSON" });

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

                // �������� �������� ������
                SaveUserSubdivisions(userId, subIds);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // ����������� ������ ������������
        public IActionResult OnPostRegisterUser()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = reader.ReadToEndAsync().Result;

                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "���������� JSON" });

                string fio = data["fullName"].ToString();
                string login = data["login"].ToString();
                string password = data["password"].ToString();
                bool canClose = Convert.ToBoolean(data["canCloseWork"]);
                bool canSend = Convert.ToBoolean(data["canSendCloseRequest"]);
                bool canSettings = Convert.ToBoolean(data["canAccessSettings"]);

                // ������: �������� ������
                // _userSettingsService.CreateUser(fio, login, password, canClose, canSend, canSettings);
                // ����� � ������ �����:
                if (string.IsNullOrWhiteSpace(login))
                    return new JsonResult(new { success = false, message = "����� ����." });

                // ����� �������� � ��...
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // -------------------------------------------------
        // ���� "���" ������, ������� ��������� ������ � ��
        // -------------------------------------------------

        private List<UserDto> LoadAllUsers()
        {
            // ��������� �������� ������ ������������� �� ����
            return new List<UserDto>
            {
                new UserDto { UserId = 1, DisplayName = "������ ����" },
                new UserDto { UserId = 2, DisplayName = "������ ����" },
                new UserDto { UserId = 3, DisplayName = "������� ������" },
                new UserDto { UserId = 4, DisplayName = "������������ ����" }
            };
        }

        private PrivacySettingsDto LoadUserPrivacySettings(int userId)
        {
            // ���������, ��� ����� �� ���� ���������
            // � ����������: SELECT ... FROM UserPrivacySettings WHERE userId = ...
            // ��� ����������� ���������� �����-�� �������� ������
            return new PrivacySettingsDto
            {
                CanCloseWork = (userId % 2 == 1),
                CanSendCloseRequest = true,
                CanAccessSettings = (userId == 1) // ��������, ������ � userId=1 ��������
            };
        }

        private void SaveUserPrivacySettings(int userId, bool canClose, bool canSend, bool canSettings)
        {
            // ���-�����: UPDATE UserPrivacySettings SET ...
            // ��������, ������� ���������
        }

        private List<SubdivisionDto> LoadAllSubdivisions()
        {
            // ��������� ������ �������������
            return new List<SubdivisionDto>
            {
                new SubdivisionDto{ Id=10, Name="����� 10"},
                new SubdivisionDto{ Id=11, Name="����� 11"},
                new SubdivisionDto{ Id=12, Name="����� 12"},
                new SubdivisionDto{ Id=13, Name="����� 13"},
            };
        }

        private List<int> LoadUserSubdivisions(int userId)
        {
            // ���������, ��� ������������ userId ����� �������� ��������� �������������
            // � �������� ������� � SELECT ... FROM UserAllowedSubdivisions WHERE userId=...
            if (userId == 1) return new List<int> { 10, 12 };
            if (userId == 2) return new List<int> { 11 };
            return new List<int>();
        }

        private void SaveUserSubdivisions(int userId, List<int> subIds)
        {
            // ���: UPDATE UserAllowedSubdivisions ...
        }
    }
}