// Monitoring.Infrastructure/Services/WorkItemService.cs
using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Реализация IWorkItemService.
    /// Здесь мы ходим в базу данных (через SqlConnection)
    /// и/или используем кэш (IMemoryCache).
    /// </summary>
    public class WorkItemService : IWorkItemService
    {
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;

        public WorkItemService(IConfiguration configuration, IMemoryCache cache)
        {
            _configuration = configuration;
            _cache = cache;
        }

        public async Task<List<WorkItem>> GetAllWorkItemsAsync(int divisionId)
        {
            // Например, получаем connection string из appsettings.json
            // или хардкодим (не рекомендуется).
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            // Ключ кэша, чтобы не гонять SQL при каждом вызове
            string cacheKey = $"AllWorkItems_{divisionId}";

            if (!_cache.TryGetValue(cacheKey, out List<WorkItem> workItems))
            {
                workItems = new List<WorkItem>();

                // Тут твой код SQL (как раньше у тебя был LoadWorkItemsFromDatabaseAsync)
                using (var conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT 
                                d.Number,
                                wu.idWork,
                                td.Name + ' ' + d.Name AS DocumentName,
                                w.Name AS WorkName,

                                -- Вместо агрегированной строки исполнителей
                                -- выводим имя конкретного пользователя
                                U2.smallName AS Executor,

                                (SELECT smallName 
                                 FROM Users 
                                 WHERE idUser = wucontr.idUser
                                ) AS Controller,

                                (SELECT smallName 
                                 FROM Users 
                                 WHERE idUser = wuc.idUser
                                ) AS Approver,

                                w.DatePlan,
                                wu.DateKorrect1,
                                wu.DateKorrect2,
                                wu.DateKorrect3,
                                w.DateFact

                            FROM WorkUser wu
                                INNER JOIN Works w 
                                    ON wu.idWork = w.id
                                INNER JOIN Documents d 
                                    ON w.idDocuments = d.id
                                LEFT JOIN WorkUserCheck wuc 
                                    ON wuc.idWork = w.id
                                LEFT JOIN WorkUserControl wucontr 
                                    ON wucontr.idWork = w.id
                                INNER JOIN TypeDocs td 
                                    ON td.id = d.idTypeDoc
                                INNER JOIN Users u 
                                    ON wu.idUser = u.idUser

                                -- Новые JOIN-ы, отвечающие за получение исполнителей
                                INNER JOIN WorkUser       wu2   ON wu2.idWork = w.id
                                INNER JOIN Users          U2    ON U2.idUser = wu2.idUser
                                INNER JOIN WorkUserCheck  wuc2  ON wuc2.idWork = w.id

                            WHERE
                                wu.dateFact IS NULL
                                AND wu.idUser IN (
                                    SELECT idUser 
                                    FROM Users 
                                    WHERE idDivision = @divId
                                );
                                    ";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@divId", divisionId);
                        await conn.OpenAsync();

                        var dict = new Dictionary<string, WorkItem>();

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                // Считываем поля
                                string idWork = reader["idWork"]?.ToString();
                                string docNumber = reader["Number"]?.ToString() + "/" + reader["idWork"]?.ToString();
                                string docName = reader["DocumentName"]?.ToString();
                                string workName = reader["WorkName"]?.ToString();
                                string executor = reader["Executor"]?.ToString();
                                string controller = reader["Controller"]?.ToString();
                                string approver = reader["Approver"]?.ToString();
                                DateTime? planDate = reader["DatePlan"] as DateTime?;
                                DateTime? kor1 = reader["DateKorrect1"] as DateTime?;
                                DateTime? kor2 = reader["DateKorrect2"] as DateTime?;
                                DateTime? kor3 = reader["DateKorrect3"] as DateTime?;
                                DateTime? factDate = reader["DateFact"] as DateTime?;

                                // Формируем ключ без поля controller (и executor), 
                                // чтобы все их варианты собирались в одну запись:
                                string key = $"{docName}|{workName}|{approver}|{planDate}|{kor1}|{kor2}|{kor3}|{factDate}|{idWork}";

                                if (!dict.ContainsKey(key))
                                {
                                    dict[key] = new WorkItem
                                    {
                                        DocumentNumber = docNumber,
                                        DocumentName = docName ?? "",
                                        WorkName = workName ?? "",
                                        // Executor и Controller пока задаём напрямую
                                        // (если придёт первая запись с ними).
                                        Executor = executor ?? "",
                                        Controller = controller ?? "",
                                        Approver = approver ?? "",
                                        PlanDate = planDate,
                                        Korrect1 = kor1,
                                        Korrect2 = kor2,
                                        Korrect3 = kor3,
                                        FactDate = factDate
                                    };
                                }
                                else
                                {
                                    // Если запись уже есть, дополняем &laquo;исполнителей&raquo; и &laquo;контролирующих&raquo;.
                                    var existing = dict[key];

                                    // *** Агрегация исполнителя (Executor) ***
                                    if (!string.IsNullOrWhiteSpace(executor))
                                    {
                                        var execList = existing.Executor
                                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                            .Select(x => x.Trim())
                                            .ToList();

                                        if (!execList.Contains(executor))
                                        {
                                            execList.Add(executor);
                                            existing.Executor = string.Join(", ", execList);
                                        }
                                    }

                                    // *** Агрегация контролирующего (Controller) ***
                                    if (!string.IsNullOrWhiteSpace(controller))
                                    {
                                        var ctrlList = existing.Controller
                                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                            .Select(x => x.Trim())
                                            .ToList();

                                        if (!ctrlList.Contains(controller))
                                        {
                                            ctrlList.Add(controller);
                                            existing.Controller = string.Join(", ", ctrlList);
                                        }
                                    }
                                }
                            }
                        }

                        workItems = dict.Values.ToList();
                    }
                }

                // Пишем в кэш
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    // Например, SlidingExpiration = ...
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                };
                _cache.Set(cacheKey, workItems, cacheOptions);
            }

            return workItems;
        }

        public async Task<List<string>> GetExecutorsAsync(int divisionId)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            string cacheKey = $"Executors_{divisionId}";

            if (!_cache.TryGetValue(cacheKey, out List<string> executors))
            {
                executors = new List<string>();

                using (var conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT DISTINCT u.smallName AS ExecName
                        FROM Users u
                        WHERE u.idDivision = @divId
                          AND u.Isvalid = 1
                        ORDER BY u.smallName
                    ";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@divId", divisionId);
                        await conn.OpenAsync();
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                executors.Add(reader["ExecName"]?.ToString() ?? "");
                            }
                        }
                    }
                }

                // кэшируем
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                };
                _cache.Set(cacheKey, executors, cacheOptions);
            }

            return executors;
        }

        public async Task<string> GetDevAsync(int divisionId)
        {
            // Это получение "названия подразделения"
            // Сюда можно тоже добавить кэш при желании
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            string dev = $"Отдел #{divisionId}";

            using (var conn = new SqlConnection(connectionString))
            {
                string query = @"SELECT smallNameDivision FROM Divisions WHERE idDivision = @divId";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@divId", divisionId);
                    await conn.OpenAsync();
                    object result = await cmd.ExecuteScalarAsync();
                    if (result != null)
                        dev = result.ToString();
                }
            }

            return dev;
        }

        public void ClearCache(int divisionId)
        {
            string cacheKey = $"AllWorkItems_{divisionId}";
            _cache.Remove(cacheKey);

            string exKey = $"Executors_{divisionId}";
            _cache.Remove(exKey);
        }
    }
}