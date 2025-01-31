using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; // ��� ������ � ���� / ������� (������� �� ������ ASP.NET Core)

namespace web_test.Pages
{
    public class LoginModel : PageModel
    {
        // ������ ���� ������������� (�� smallName) ��� ����������� ������
        public List<string> AllUsers { get; set; } = new List<string>();

        // ����, ���� ������ ���������� ������������ (�������� select)
        [BindProperty]
        public string SelectedUser { get; set; }

        // ���� ��� ������
        [BindProperty]
        public string Password { get; set; }

        // ������ ����������� (���� ����� ���������)
        public string ErrorMessage { get; set; }

        // ������������ � ��
        private readonly string connectionString = "Data Source=ASCON;Initial Catalog=DocumentControl;Persist Security Info=False;User ID=test;Password=test123456789";

        // ��� ������ �� �������� (GET) ��������� ������ �������������
        public async Task OnGet()
        {
            await LoadAllUsersAsync();
        }

        // ��� �������� ����� (POST) ���� �������� ������������� (����� �� ��������� �� ������), 
        // � �������� �����������
        public async Task<IActionResult> OnPost()
        {
            await LoadAllUsersAsync();  // ����� ��� ������� �� ����� �������� ������ �������������

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

            // ��������� � �� ������ � idDivision
            int? divisionIdFromDb = null;
            bool isPasswordValid = false;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                // �������� ������ � idDivision �� ���������� smallName
                string query = @"
                    SELECT Password, idDivision 
                    FROM [Users]
                    WHERE smallName = @userName
                      AND Isvalid = 1  -- ����� � ��� ���� ������� ����������
                ";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@userName", SelectedUser);

                    await conn.OpenAsync();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string passwordFromDb = reader["Password"]?.ToString();
                            divisionIdFromDb = reader["idDivision"] as int?;

                            // ��������� ������
                            if (passwordFromDb == Password)
                            {
                                isPasswordValid = true;
                            }
                        }
                    }
                }
            }

            if (!isPasswordValid || divisionIdFromDb == null)
            {
                ErrorMessage = "�������� ��� ������������ ��� ������.";
                return Page();
            }

            // ���� ������ ������, ��������� ������ � ������������.
            // ������� 1. ��������� divisionId ��� �������� ��� ���������:
            // return RedirectToPage("Index", new { divisionId = divisionIdFromDb });

            // ������� 2. ��������� � cookie � ������ ���������� �� Index:
            HttpContext.Response.Cookies.Append("userName", SelectedUser);
            HttpContext.Response.Cookies.Append("divisionId", divisionIdFromDb.ToString());

            // ����� � � ������:
            // HttpContext.Session.SetString("userName", SelectedUser);
            // HttpContext.Session.SetInt32("divisionId", divisionIdFromDb.Value);

            return RedirectToPage("Index");
            // (����� Index ���� ��� ��������� ��� ����/������ � ����������� divisionId)
        }

        private async Task LoadAllUsersAsync()
        {
            AllUsers.Clear();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"SELECT smallName FROM [Users] WHERE Isvalid = 1 ORDER BY smallName";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            AllUsers.Add(reader["smallName"].ToString());
                        }
                    }
                }
            }
        }
    }
}