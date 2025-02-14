// Monitoring.Infrastructure/Services/NotificationService.cs
using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;

namespace Monitoring.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IConfiguration _configuration;

        public NotificationService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task DeactivateOldNotificationsAsync(int days)
        {
            string connStr = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                // Вычислим дату, старше которой уведомления считаем &laquo;старыми&raquo;
                DateTime cutoffDate = DateTime.Today.AddDays(-days);

                string updateSql = @"
                    UPDATE DocumentControl.dbo.messageView
                    SET isActive = 0
                    WHERE dateSetInSystem < @Cutoff
                ";

                using (SqlCommand cmd = new SqlCommand(updateSql, conn))
                {
                    cmd.Parameters.AddWithValue("@Cutoff", cutoffDate);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<List<Notification>> GetActiveNotificationsAsync(int divisionId)
        {
            string connStr = _configuration.GetConnectionString("DefaultConnection");
            var result = new List<Notification>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();

                string selectSql = @"
                    SELECT mV.id, mV.name, mV.dateSetInSystem, u.smallName, mV.isActive
                    FROM DocumentControl.dbo.messageView mV
                    INNER JOIN Users u ON u.idUser = mV.idUser
                    WHERE mV.idUser IN (
                        SELECT idUser FROM Users WHERE idDivision = @divisionId
                    )
                    AND mV.isActive = 1
                    ORDER BY mV.dateSetInSystem DESC
                ";

                using (SqlCommand cmd = new SqlCommand(selectSql, conn))
                {
                    cmd.Parameters.AddWithValue("@divisionId", divisionId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(new Notification
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                Title = reader.GetString(reader.GetOrdinal("name")),
                                DateSetInSystem = reader.GetDateTime(reader.GetOrdinal("dateSetInSystem")),
                                UserName = reader.GetString(reader.GetOrdinal("smallName")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("isActive"))
                            });
                        }
                    }
                }
            }

            return result;
        }
    }
}