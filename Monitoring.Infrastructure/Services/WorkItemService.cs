using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Реализация IWorkItemService.
    /// Ходим в базу данных (через SqlConnection) и используем IMemoryCache.
    /// </summary>
    public class WorkItemService : IWorkItemService
    {
        private readonly IMemoryCache _cache;
        private readonly string _connectionString;

        public WorkItemService(IConfiguration configuration, IMemoryCache cache)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Connection string not found");
            _cache = cache;
        }

        /// <summary>
        /// Получить все WorkItems для указанных подразделений (divisionIds).
        /// Результат кэшируется с учётом списка divisionIds.
        /// </summary>
        public async Task<List<WorkItem>> GetAllWorkItemsAsync(List<int> divisionIds)
        {
            // Если список пустой, возвращаем пустой список (или можно выбросить исключение)
            if (divisionIds == null || divisionIds.Count == 0)
                return new List<WorkItem>();

            // Чтобы кэшировать по комбинации подразделений, упорядочим ID 
            // и сформируем строку-ключ: "AllWorkItems_10_12_17"
            var sortedDivs = divisionIds.OrderBy(x => x).ToList();
            string divisionsKeyPart = string.Join("_", sortedDivs);
            string cacheKey = $"AllWorkItems_{divisionsKeyPart}";

            // Пытаемся взять из кэша
            if (!_cache.TryGetValue(cacheKey, out List<WorkItem> workItems))
            {
                workItems = new List<WorkItem>();

                // Динамическое формирование IN (...)
                var paramNames = new List<string>();
                for (int i = 0; i < sortedDivs.Count; i++)
                {
                    paramNames.Add($"@div{i}");
                }
                string inClause = string.Join(", ", paramNames);

                string query = $@"
                    SELECT 
                        d.Number,
                        wu.idWork,
                        td.Name + ' ' + d.Name AS DocumentName,
                        w.Name AS WorkName,
                        -- Имя конкретного пользователя-исполнителя
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

                        -- Дополнительные JOIN-ы (EXECUTOR):
                        INNER JOIN WorkUser       wu2   ON wu2.idWork = w.id
                        INNER JOIN Users          U2    ON U2.idUser = wu2.idUser
                        INNER JOIN WorkUserCheck  wuc2  ON wuc2.idWork = w.id

                    WHERE
                        wu.dateFact IS NULL
                        AND wu.idUser IN (
                            SELECT idUser 
                            FROM Users 
                            WHERE idDivision IN ({inClause})
                        );
                ";

                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(query, conn))
                {
                    // Добавляем параметры
                    for (int i = 0; i < sortedDivs.Count; i++)
                    {
                        cmd.Parameters.AddWithValue(paramNames[i], sortedDivs[i]);
                    }

                    await conn.OpenAsync();

                    // Используем словарь для "агрегации" записей:
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

                            // Формируем ключ для агрегации (без executor/controller),
                            // чтобы одинаковые записи объединять:
                            string key = $"{docName}|{workName}|{approver}|{planDate}|{kor1}|{kor2}|{kor3}|{factDate}|{idWork}";

                            if (!dict.ContainsKey(key))
                            {
                                dict[key] = new WorkItem
                                {
                                    DocumentNumber = docNumber,
                                    DocumentName = docName ?? "",
                                    WorkName = workName ?? "",
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
                                // Если запись уже есть, "добавляем" исполнителей/контроллеров
                                var existing = dict[key];

                                // Агрегация исполнителей (Executor)
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

                                // Агрегация контролирующих (Controller)
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

                    // Преобразуем словарь в список
                    workItems = dict.Values.ToList();
                }

                // Сохраняем в кэш
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                };
                _cache.Set(cacheKey, workItems, cacheOptions);
            }

            return workItems;
        }

        /// <summary>
        /// Получить список исполнителей (smallName) внутри одного отдела.
        /// (Пока оставляем как было. Если понадобится — можно расширить для списка отделов)
        /// </summary>
        public async Task<List<string>> GetExecutorsAsync(int divisionId)
        {
            string cacheKey = $"Executors_{divisionId}";

            if (!_cache.TryGetValue(cacheKey, out List<string> executors))
            {
                executors = new List<string>();

                using (var conn = new SqlConnection(_connectionString))
                {
                    string query = @"
                        SELECT DISTINCT u.smallName AS ExecName
                        FROM [DocumentControl].[dbo].[Users] u
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

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                };
                _cache.Set(cacheKey, executors, cacheOptions);
            }

            return executors;
        }

        /// <summary>
        /// Получить "название" отдела (smallNameDivision) по idDivision.
        /// </summary>
        public async Task<string> GetDevAsync(int divisionId)
        {
            string dev = $"Отдел #{divisionId}";

            using (var conn = new SqlConnection(_connectionString))
            {
                string query = @"SELECT smallNameDivision 
                                 FROM [DocumentControl].[dbo].[Divisions] 
                                 WHERE idDivision = @divId";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@divId", divisionId);
                    await conn.OpenAsync();
                    object result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                        dev = result.ToString();
                }
            }

            return dev;
        }

        /// <summary>
        /// Очистить кэш для конкретного отдела (или для любого).
        /// </summary>
        public void ClearCache(int divisionId)
        {
            string cacheKey = $"AllWorkItems_{divisionId}";
            _cache.Remove(cacheKey);

            string exKey = $"Executors_{divisionId}";
            _cache.Remove(exKey);

            // Если у вас есть кэш по комбинациям отделов,
            // то можно пройтись по всем ключам в MemoryCache и удалить, 
            // где встречается divisionId.
        }
    }
}