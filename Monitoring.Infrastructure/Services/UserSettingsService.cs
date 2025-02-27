using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Monitoring.Infrastructure.Services
{
    public class UserSettingsService : IUserSettingsService
    {
        private readonly IConfiguration _configuration;

        public UserSettingsService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> HasAccessToSettingsAsync(int userId)
        {
            var settings = await GetPrivacySettingsAsync(userId);
            return settings != null && settings.CanAccessSettings;
        }

        public async Task<bool> HasAccessToSendCloseRequestAsync(int userId)
        {
            var settings = await GetPrivacySettingsAsync(userId);
            return settings != null && settings.CanSendCloseRequest;
        }

        public async Task<bool> HasAccessToCloseWorkAsync(int userId)
        {
            var settings = await GetPrivacySettingsAsync(userId);
            return settings != null && settings.CanCloseWork;
        }

        public async Task<PrivacySettingsDto> GetPrivacySettingsAsync(int userId)
        {
            var result = new PrivacySettingsDto
            {
                CanCloseWork = false,
                CanSendCloseRequest = false,
                CanAccessSettings = false
            };

            string connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                string query = @"
                    SELECT [CanCloseWork],
                           [CanSendCloseRequest],
                           [CanAccessSettings]
                    FROM [UserPrivacy]
                    WHERE [idUser] = @u
                ";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@u", userId);
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            result.CanCloseWork = reader.GetBoolean(0);
                            result.CanSendCloseRequest = reader.GetBoolean(1);
                            result.CanAccessSettings = reader.GetBoolean(2);
                        }
                    }
                }
            }

            return result;
        }

        // ВАЖНО! Обратите внимание, что мы добавляем параметр "bool isActive"
        // и перед сохранением в [UserPrivacy] обновляем ещё и поле [Isvalid] в [Users].
        public async Task SavePrivacySettingsAsync(int userId, PrivacySettingsDto dto, bool isActive)
        {
            string connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1) Обновляем таблицу [Users], поле [Isvalid]
                        //    (1 = активен, 0 = неактивен)
                        string updateUserIsValid = @"
                            UPDATE [Users]
                            SET [Isvalid] = @isv
                            WHERE [idUser] = @u
                        ";
                        using (var cmdVal = new SqlCommand(updateUserIsValid, conn, transaction))
                        {
                            cmdVal.Parameters.AddWithValue("@isv", isActive ? 1 : 0);
                            cmdVal.Parameters.AddWithValue("@u", userId);
                            await cmdVal.ExecuteNonQueryAsync();
                        }

                        // 2) Смотрим, есть ли запись в [UserPrivacy]
                        string selectQuery = @"SELECT COUNT(*) FROM [UserPrivacy] WHERE [idUser] = @u";
                        using (var cmdSelect = new SqlCommand(selectQuery, conn, transaction))
                        {
                            cmdSelect.Parameters.AddWithValue("@u", userId);
                            int count = (int)await cmdSelect.ExecuteScalarAsync();

                            if (count == 0)
                            {
                                // INSERT
                                string insertQuery = @"
                                    INSERT INTO [UserPrivacy]
                                        ([idUser], [CanCloseWork], [CanSendCloseRequest], [CanAccessSettings])
                                    VALUES
                                        (@u, @close, @send, @acc)
                                ";
                                using (var cmdInsert = new SqlCommand(insertQuery, conn, transaction))
                                {
                                    cmdInsert.Parameters.AddWithValue("@u", userId);
                                    cmdInsert.Parameters.AddWithValue("@close", dto.CanCloseWork);
                                    cmdInsert.Parameters.AddWithValue("@send", dto.CanSendCloseRequest);
                                    cmdInsert.Parameters.AddWithValue("@acc", dto.CanAccessSettings);
                                    await cmdInsert.ExecuteNonQueryAsync();
                                }
                            }
                            else
                            {
                                // UPDATE
                                string updateQuery = @"
                                    UPDATE [UserPrivacy]
                                    SET [CanCloseWork] = @close,
                                        [CanSendCloseRequest] = @send,
                                        [CanAccessSettings] = @acc
                                    WHERE [idUser] = @u
                                ";
                                using (var cmdUpdate = new SqlCommand(updateQuery, conn, transaction))
                                {
                                    cmdUpdate.Parameters.AddWithValue("@u", userId);
                                    cmdUpdate.Parameters.AddWithValue("@close", dto.CanCloseWork);
                                    cmdUpdate.Parameters.AddWithValue("@send", dto.CanSendCloseRequest);
                                    cmdUpdate.Parameters.AddWithValue("@acc", dto.CanAccessSettings);
                                    await cmdUpdate.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        // Если всё ОК — фиксируем транзакцию
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<List<DivisionDto>> GetAllDivisionsAsync()
        {
            var list = new List<DivisionDto>();
            string connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT [idDivision],
                           [idParentDivision],
                           [NameDivision],
                           [smallNameDivision],
                           [position],
                           [idUserHead]
                    FROM [Divisions]
                    ORDER BY [idDivision]
                ";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var d = new DivisionDto
                            {
                                IdDivision = reader.GetInt32(0),
                                IdParentDivision = !reader.IsDBNull(1) ? reader.GetInt32(1) : (int?)null,
                                NameDivision = reader.GetString(2),
                                SmallNameDivision = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Position = !reader.IsDBNull(4) ? reader.GetInt32(4) : (int?)null,
                                IdUserHead = !reader.IsDBNull(5) ? reader.GetInt32(5) : (int?)null
                            };
                            list.Add(d);
                        }
                    }
                }
            }
            return list;
        }

        public async Task<List<int>> GetUserAllowedDivisionsAsync(int userId)
        {
            var list = new List<int>();
            string connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                string sql = @"
                    SELECT [idDivision]
                    FROM [UserAllowedDivisions]
                    WHERE [idUser] = @u
                ";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", userId);
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(reader.GetInt32(0));
                        }
                    }
                }
            }
            return list;
        }

        public async Task SaveUserAllowedDivisionsAsync(int userId, List<int> divisionIds)
        {
            string connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1) Удаляем все старые записи
                        string deleteSql = @"
                            DELETE FROM [UserAllowedDivisions]
                            WHERE [idUser] = @u
                        ";
                        using (var cmdDel = new SqlCommand(deleteSql, conn, transaction))
                        {
                            cmdDel.Parameters.AddWithValue("@u", userId);
                            await cmdDel.ExecuteNonQueryAsync();
                        }

                        // 2) Вставляем новые
                        string insertSql = @"
                            INSERT INTO [UserAllowedDivisions]([idUser],[idDivision])
                            VALUES(@u, @d)
                        ";
                        foreach (var divId in divisionIds)
                        {
                            using (var cmdIns = new SqlCommand(insertSql, conn, transaction))
                            {
                                cmdIns.Parameters.AddWithValue("@u", userId);
                                cmdIns.Parameters.AddWithValue("@d", divId);
                                await cmdIns.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task ChangeUserPasswordAsync(int userId, string newPassword)
        {
            string connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                string sql = @"
                    UPDATE [Users]
                    SET [Password] = @pwd
                    WHERE [idUser] = @u
                ";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@pwd", newPassword);
                    cmd.Parameters.AddWithValue("@u", userId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<string?> GetUserCurrentPasswordAsync(int userId)
        {
            string? password = null;
            string connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                string sql = @"
                    SELECT [Password]
                    FROM [Users]
                    WHERE [idUser] = @u
                ";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", userId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            password = reader.IsDBNull(0) ? null : reader.GetString(0);
                        }
                    }
                }
            }
            return password;
        }

        public async Task<int> RegisterUserInDbAsync(
            string fullName,
            string smallName,
            string password,
            int? idDivision,
            bool canCloseWork,
            bool canSendCloseRequest,
            bool canAccessSettings
        )
        {
            int newUserId = 0;
            string connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1) INSERT в [Users]
                        string insertUserSql = @"
                            INSERT INTO [Users]
                                ([Name], [smallName], [idDivision], [Password], [idTypeUser], [Isvalid])
                            OUTPUT INSERTED.[idUser]
                            VALUES
                                (@name, @smallName, @idDiv, @pwd, 2, 1)
                        ";
                        using (var cmd = new SqlCommand(insertUserSql, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@name", fullName);
                            cmd.Parameters.AddWithValue("@smallName", (object?)smallName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@idDiv", (object?)idDivision ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@pwd", password);

                            object newIdObj = await cmd.ExecuteScalarAsync();
                            newUserId = Convert.ToInt32(newIdObj);
                        }

                        // 2) INSERT в [UserPrivacy]
                        string insertPrivacySql = @"
                            INSERT INTO [UserPrivacy]
                                ([idUser], [CanCloseWork], [CanSendCloseRequest], [CanAccessSettings])
                            VALUES
                                (@u, @cClose, @cSend, @cAccess)
                        ";
                        using (var cmdP = new SqlCommand(insertPrivacySql, conn, transaction))
                        {
                            cmdP.Parameters.AddWithValue("@u", newUserId);
                            cmdP.Parameters.AddWithValue("@cClose", canCloseWork);
                            cmdP.Parameters.AddWithValue("@cSend", canSendCloseRequest);
                            cmdP.Parameters.AddWithValue("@cAccess", canAccessSettings);
                            await cmdP.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            return newUserId;
        }
    }
}