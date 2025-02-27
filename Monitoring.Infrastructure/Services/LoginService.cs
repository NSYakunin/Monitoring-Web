using Monitoring.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Реализация ILoginService.
    /// Код, напрямую работающий с БД [Users].
    /// </summary>
    public class LoginService : ILoginService
    {
        private readonly IConfiguration _configuration;

        public LoginService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Получить список smallName всех активных пользователей (Isvalid=1).
        /// </summary>
        public async Task<List<string>> GetAllUsersAsync()
        {
            var users = new List<string>();
            string connStr = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string query = @"
                    SELECT [smallName]
                    FROM [DocumentControl].[dbo].[Users]
                    WHERE [Isvalid] = 1
                    ORDER BY [smallName]
                ";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            users.Add(reader["smallName"]?.ToString() ?? "");
                        }
                    }
                }
            }

            return users;
        }

        /// <summary>
        /// Новый метод: получить список smallName только неактивных (Isvalid=0) пользователей.
        /// </summary>
        public async Task<List<string>> GetAllInactiveUsersAsync()
        {
            var users = new List<string>();
            string connStr = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string query = @"
                    SELECT [smallName]
                    FROM [DocumentControl].[dbo].[Users]
                    WHERE [Isvalid] = 0
                    ORDER BY [smallName]
                ";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            users.Add(reader["smallName"]?.ToString() ?? "");
                        }
                    }
                }
            }

            return users;
        }

        /// <summary>
        /// Фильтрует пользователей по подстроке (LIKE '%query%').
        /// </summary>
        public async Task<List<string>> FilterUsersAsync(string query)
        {
            if (query == null) query = "";

            var matchedUsers = new List<string>();
            string connStr = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT [smallName]
                    FROM [DocumentControl].[dbo].[Users]
                    WHERE [Isvalid] = 1
                      AND [smallName] LIKE '%' + @q + '%'
                    ORDER BY [smallName]
                ";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@q", query);
                    await conn.OpenAsync();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            matchedUsers.Add(reader["smallName"]?.ToString() ?? "");
                        }
                    }
                }
            }

            return matchedUsers;
        }

        /// <summary>
        /// Проверяет логин/пароль, возвращая (idDivision, isValid).
        /// Если всё ок, isValid = true и возвращается divisionId пользователя.
        /// </summary>
        public async Task<(int? divisionId, bool isValid)> CheckUserCredentialsAsync(
            string selectedUser,
            string password
        )
        {
            if (string.IsNullOrEmpty(selectedUser) || string.IsNullOrEmpty(password))
                return (null, false);

            int? divisionIdFromDb = null;
            bool isPasswordValid = false;

            string connStr = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                // Выбираем пароль и idDivision по выбранному smallName
                string query = @"
                    SELECT [Password], [idDivision]
                    FROM [DocumentControl].[dbo].[Users]
                    WHERE [smallName] = @userName
                      AND [Isvalid] = 1
                ";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@userName", selectedUser);

                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string? passwordFromDb = reader["Password"]?.ToString();
                            divisionIdFromDb = reader["idDivision"] as int?;

                            if (passwordFromDb == password)
                            {
                                isPasswordValid = true;
                            }
                        }
                    }
                }
            }

            if (divisionIdFromDb.HasValue && isPasswordValid)
                return (divisionIdFromDb, true);

            return (null, false);
        }

        /// <summary>
        /// Возвращает idUser по полю smallName.
        /// Если такой пользователь не найден — вернёт null.
        /// </summary>
        public async Task<int> GetUserIdByNameAsync(string userName)
        {
            int userId = 0;
            string connStr = _configuration.GetConnectionString("DefaultConnection");

            using (var conn = new SqlConnection(connStr))
            {
                string query = @"
                    SELECT [idUser]
                    FROM [DocumentControl].[dbo].[Users]
                    WHERE [smallName] = @UserName
                ";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserName", userName);
                    await conn.OpenAsync();

                    object result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        userId = Convert.ToInt32(result);
                    }
                }
            }

            return userId;
        }
    }
}