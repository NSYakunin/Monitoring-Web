using Monitoring.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;
using Monitoring.Application.DTO;

namespace Monitoring.Infrastructure.Services
{
    public class UserSettingsService : IUserSettingsService
    {
        private readonly IConfiguration _configuration;

        public UserSettingsService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>Проверяет, есть ли у пользователя доступ к настройкам</summary>
        public async Task<bool> HasAccessToSettingsAsync(int userId)
        {
            var settings = await GetPrivacySettingsAsync(userId);
            return settings != null && settings.CanAccessSettings;
        }

        /// <summary>Получает объект PrivacySettingsDto из таблицы UserPrivacy</summary>
        public async Task<PrivacySettingsDto> GetPrivacySettingsAsync(int userId)
        {
            var result = new PrivacySettingsDto
            {
                // По умолчанию все false
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

        /// <summary>Сохранить настройки приватности (CanCloseWork / CanSendCloseRequest / CanAccessSettings)</summary>
        public async Task SavePrivacySettingsAsync(int userId, PrivacySettingsDto dto)
        {
            // Нужно проверить, есть ли запись в таблице UserPrivacy
            // Если нет, сделать INSERT, если да — UPDATE.
            string connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                string selectQuery = @"SELECT COUNT(*) FROM [UserPrivacy] WHERE [idUser] = @u";
                using (var cmd = new SqlCommand(selectQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@u", userId);
                    await conn.OpenAsync();
                    int count = (int)await cmd.ExecuteScalarAsync();

                    if (count == 0)
                    {
                        // INSERT
                        string insertQuery = @"
                            INSERT INTO [UserPrivacy]
                                ([idUser], [CanCloseWork], [CanSendCloseRequest], [CanAccessSettings])
                            VALUES
                                (@u, @close, @send, @acc)
                        ";
                        using (var cmdInsert = new SqlCommand(insertQuery, conn))
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
                        using (var cmdUpdate = new SqlCommand(updateQuery, conn))
                        {
                            cmdUpdate.Parameters.AddWithValue("@u", userId);
                            cmdUpdate.Parameters.AddWithValue("@close", dto.CanCloseWork);
                            cmdUpdate.Parameters.AddWithValue("@send", dto.CanSendCloseRequest);
                            cmdUpdate.Parameters.AddWithValue("@acc", dto.CanAccessSettings);
                            await cmdUpdate.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }

        /// <summary>Загрузить список ВСЕХ подразделений (Divisions) из таблицы Divisions</summary>
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

        /// <summary>Получить список idDivision, к которым у пользователя есть доступ (UserAllowedDivisions)</summary>
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

        /// <summary>Сохранить список подразделений, разрешённых пользователю.</summary>
        public async Task SaveUserAllowedDivisionsAsync(int userId, List<int> divisionIds)
        {
            string connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                // Начнём транзакцию, чтобы все операции прошли атомарно.
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

                        // 2) Вставляем заново все новые
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
    }
}
