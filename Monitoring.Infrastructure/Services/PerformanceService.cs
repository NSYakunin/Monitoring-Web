using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using DocumentFormat.OpenXml.Drawing.Charts;
using Microsoft.Extensions.Configuration;
using Monitoring.Application.Interfaces;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Сервис для получения данных по исполнению из базы данных.
    /// </summary>
    public class PerformanceService : IPerformanceService
    {
        private readonly IConfiguration _configuration;

        public PerformanceService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Загружает данные по исполнению (план, факт, % исполнения) за указанный период.
        /// </summary>
        public List<PerformanceDto> GetPerformanceData(DateTime startDate, DateTime endDate)
        {
            var result = new List<PerformanceDto>();

            // Тот самый запрос, разбитый на подзапросы для Плана и Факта.
            // Обратите внимание, что здесь всё параметризовано (@StartDate, @EndDate).
            string sql = @"
                SELECT
                    d.idDivision,
                    d.NameDivision AS [Подразделение],

                    -- ФАКТ (кол-во уникальных закрытых работ за период)
                    ISNULL(f.FactCount,0) AS [Факт],

                    -- ПЛАН (невыполненные + фактически выполненные за период)
                    ISNULL(p.PlanCount,0) + ISNULL(f.FactCount,0) AS [План],

                    -- ПРОЦЕНТ (Факт / План)
                    CASE WHEN (ISNULL(p.PlanCount,0) + ISNULL(f.FactCount,0)) = 0 THEN 0
                         ELSE 1.0 * ISNULL(f.FactCount,0) 
                                 / (ISNULL(p.PlanCount,0) + ISNULL(f.FactCount,0))
                    END AS [%_Исполнения]
                FROM DocumentControl.dbo.Divisions d
                LEFT JOIN
                (
                    -- Подзапрос для плановых (невыполненных) работ
                    SELECT 
                        u.idDivision,
                        COUNT(DISTINCT w.id) AS PlanCount
                    FROM WorkUser wu
                    INNER JOIN Works     w   ON w.id   = wu.idWork
                    INNER JOIN Documents doc ON doc.id = w.idDocuments
                    INNER JOIN Users     u   ON u.idUser = wu.idUser
                    WHERE 
                        doc.idTypeDoc <> 15                  -- Исключить некоторые типы, если нужно
                        AND (wu.dateFact IS NULL OR wu.dateFact > @EndDate)
                        AND w.DatePlan <= @EndDate
                        -- Проверка последней корректировки по плану:
                        AND (
                              CASE 
                                WHEN wu.dateKorrect3 IS NOT NULL THEN wu.dateKorrect3
                                WHEN wu.dateKorrect2 IS NOT NULL THEN wu.dateKorrect2
                                WHEN wu.dateKorrect1 IS NOT NULL THEN wu.dateKorrect1
                                ELSE '1900-01-01'
                              END
                            ) <= @EndDate
                    GROUP BY
                        u.idDivision
                ) p ON p.idDivision = d.idDivision
                LEFT JOIN
                (
                    -- Подзапрос для фактически выполненных в интервале
                    SELECT
                        u.idDivision,
                        COUNT(DISTINCT w.id) AS FactCount
                    FROM WorkUser wu
                    INNER JOIN Works     w   ON w.id   = wu.idWork
                    INNER JOIN Documents doc ON doc.id = w.idDocuments
                    INNER JOIN Users     u   ON u.idUser = wu.idUser
                    WHERE 
                        doc.idTypeDoc <> 15
                        AND wu.dateFact BETWEEN @StartDate AND @EndDate
                    GROUP BY
                        u.idDivision
                ) f ON f.idDivision = d.idDivision
                WHERE
                    d.position IS NOT NULL
                ORDER BY
                    d.position; ";

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                // Параметры для периода
                cmd.Parameters.AddWithValue("@StartDate", startDate);
                cmd.Parameters.AddWithValue("@EndDate", endDate);

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var divisionId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        var divisionName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        var factCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        var planCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        var percentage = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4);

                        result.Add(new PerformanceDto
                        {
                            DivisionId = divisionId,
                            DivisionName = divisionName,
                            PlanCount = planCount,
                            FactCount = factCount,
                            // Запишем в поле Percentage в виде десятичной дроби (0.27 = 27%)
                            Percentage = percentage
                        });
                    }
                }
            }

            return result;
        }
    }
}