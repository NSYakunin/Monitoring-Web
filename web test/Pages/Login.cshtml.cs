using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; // Для работы с куки / сессией (зависит от версии ASP.NET Core)

namespace web_test.Pages
{
    public class LoginModel : PageModel
    {
        // Список всех пользователей (их smallName) для выпадающего списка
        public List<string> AllUsers { get; set; } = new List<string>();

        // Поле, куда биндим выбранного пользователя (значение select)
        [BindProperty]
        public string SelectedUser { get; set; }

        // Поле для пароля
        [BindProperty]
        public string Password { get; set; }

        // Ошибка авторизации (если такая возникнет)
        public string ErrorMessage { get; set; }

        // Подключаемся к БД
        private readonly string connectionString = "Data Source=ASCON;Initial Catalog=DocumentControl;Persist Security Info=False;User ID=test;Password=test123456789";

        // При заходе на страницу (GET) загружаем список пользователей
        public async Task OnGet()
        {
            await LoadAllUsersAsync();
        }

        // При отправке формы (POST) тоже загрузим пользователей (чтобы не пропадали из списка), 
        // и проверим авторизацию
        public async Task<IActionResult> OnPost()
        {
            await LoadAllUsersAsync();  // чтобы при неудаче мы опять показали список пользователей

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

            // Проверяем в БД пароль и idDivision
            int? divisionIdFromDb = null;
            bool isPasswordValid = false;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                // Выбираем пароль и idDivision по выбранному smallName
                string query = @"
                    SELECT Password, idDivision 
                    FROM [Users]
                    WHERE smallName = @userName
                      AND Isvalid = 1  -- вдруг у вас есть признак валидности
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

                            // Сравнение пароля
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
                ErrorMessage = "Неверное имя пользователя или пароль.";
                return Page();
            }

            // Если пароль верный, сохраняем данные о пользователе.
            // Вариант 1. Передадим divisionId как параметр при редиректе:
            // return RedirectToPage("Index", new { divisionId = divisionIdFromDb });

            // Вариант 2. Сохраняем в cookie и просто редиректим на Index:
            HttpContext.Response.Cookies.Append("userName", SelectedUser);
            HttpContext.Response.Cookies.Append("divisionId", divisionIdFromDb.ToString());

            // Можно и в сессию:
            // HttpContext.Session.SetString("userName", SelectedUser);
            // HttpContext.Session.SetInt32("divisionId", divisionIdFromDb.Value);

            return RedirectToPage("Index");
            // (пусть Index сама уже проверяет эти куки/сессию и подставляет divisionId)
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