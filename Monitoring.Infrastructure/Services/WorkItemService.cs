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
            if (divisionIds == null || divisionIds.Count == 0)
                return new List<WorkItem>();

            // Для нескольких отделов - объединяем
            var all = new List<WorkItem>();

            foreach (int divId in divisionIds.Distinct())
            {
                var itemsForDiv = await GetAllWorkItemsForSingleDivision(divId);
                all.AddRange(itemsForDiv);
            }

            // Теперь "агрегируем" их (собираем одинаковые записи, чтобы не дублировать исполнителей).
            // Можно использовать тот же подход со словарём (documentNumber + поля).
            var dict = new Dictionary<string, WorkItem>();

            foreach (var w in all)
            {
                // Ключ
                string key = $"{w.DocumentName}|{w.WorkName}|{w.Approver}|{w.PlanDate}|{w.Korrect1}|{w.Korrect2}|{w.Korrect3}|{w.FactDate}|{w.DocumentNumber}";

                if (!dict.ContainsKey(key))
                {
                    dict[key] = new WorkItem
                    {
                        DocumentNumber = w.DocumentNumber,
                        DocumentName = w.DocumentName,
                        WorkName = w.WorkName,
                        Executor = w.Executor,
                        Controller = w.Controller,
                        Approver = w.Approver,
                        PlanDate = w.PlanDate,
                        Korrect1 = w.Korrect1,
                        Korrect2 = w.Korrect2,
                        Korrect3 = w.Korrect3,
                        FactDate = w.FactDate
                    };
                }
                else
                {
                    // Агрегация исполнителей/контроллеров
                    var existing = dict[key];

                    if (!string.IsNullOrWhiteSpace(w.Executor))
                    {
                        var execList = existing.Executor
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Trim())
                            .ToList();

                        var newExecs = w.Executor
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Trim());

                        foreach (var ex in newExecs)
                        {
                            if (!execList.Contains(ex))
                            {
                                execList.Add(ex);
                            }
                        }
                        existing.Executor = string.Join(", ", execList);
                    }

                    if (!string.IsNullOrWhiteSpace(w.Controller))
                    {
                        var ctrlList = existing.Controller
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Trim())
                            .ToList();

                        var newCtrls = w.Controller
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Trim());

                        foreach (var c in newCtrls)
                        {
                            if (!ctrlList.Contains(c))
                            {
                                ctrlList.Add(c);
                            }
                        }
                        existing.Controller = string.Join(", ", ctrlList);
                    }
                }
            }

            return dict.Values.ToList();
        }

        /// <summary>
        /// Получить кэшированные работы для одного отдела
        /// </summary>
        private async Task<List<WorkItem>> GetAllWorkItemsForSingleDivision(int divisionId)
        {
            string cacheKey = $"AllWorkItems_div{divisionId}";
            if (_cache.TryGetValue(cacheKey, out List<WorkItem> cached))
            {
                return cached;
            }

            // Грузим из БД
            var result = new List<WorkItem>();
            using (var conn = new SqlConnection(_connectionString))
            {
                string sql = @"
                    SELECT 
                        d.Number,
                        wu.idWork,
                        td.Name + ' ' + d.Name AS DocumentName,
                        w.Name AS WorkName,
                        (SELECT smallName FROM Users WHERE idUser = wucontr.idUser ) AS Controller,
                        (SELECT smallName FROM Users WHERE idUser = wuc.idUser) AS Approver,
                        w.DatePlan,
                        wu.DateKorrect1,
                        wu.DateKorrect2,
                        wu.DateKorrect3,
                        w.DateFact,
                        -- Исполнитель:
                        u.smallName AS Executor
                    FROM WorkUser wu
                        INNER JOIN Works w ON wu.idWork = w.id
                        INNER JOIN Documents d ON w.idDocuments = d.id
                        LEFT JOIN WorkUserCheck wuc ON wuc.idWork = w.id
                        LEFT JOIN WorkUserControl wucontr ON wucontr.idWork = w.id
                        INNER JOIN TypeDocs td ON td.id = d.idTypeDoc
                        INNER JOIN Users u ON wu.idUser = u.idUser
                    WHERE wu.dateFact IS NULL
                      AND u.idDivision = @div
                      AND u.Isvalid = 1
                ";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@div", divisionId);
                    await conn.OpenAsync();

                    var listRaw = new List<WorkItem>();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string idWork = reader["idWork"]?.ToString();
                            string docNumber = reader["Number"]?.ToString() + "/" + idWork;
                            var item = new WorkItem
                            {
                                DocumentNumber = docNumber,
                                DocumentName = reader["DocumentName"]?.ToString() ?? "",
                                WorkName = reader["WorkName"]?.ToString() ?? "",
                                Executor = reader["Executor"]?.ToString() ?? "",
                                Controller = reader["Controller"]?.ToString() ?? "",
                                Approver = reader["Approver"]?.ToString() ?? "",
                                PlanDate = reader["DatePlan"] as DateTime?,
                                Korrect1 = reader["DateKorrect1"] as DateTime?,
                                Korrect2 = reader["DateKorrect2"] as DateTime?,
                                Korrect3 = reader["DateKorrect3"] as DateTime?,
                                FactDate = reader["DateFact"] as DateTime?
                            };
                            listRaw.Add(item);
                        }
                    }

                    // Теперь listRaw может содержать дубликаты той же работы с разными исполнителями,
                    // но, в отличие от мульти-отдела, здесь мы можем уже агрегировать.
                    // Для простоты можем вернуть как есть, а уже в вызывающем методе объединять.
                    result = listRaw;
                }
            }

            // Сохраняем в кэш
            var cacheOpts = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            _cache.Set(cacheKey, result, cacheOpts);

            return result;
        }

        // НОВО: Получить список "Принимающих" (smallName) внутри отдела
        public async Task<List<string>> GetApproversAsync(int divisionId)
        {
            string cacheKey = $"Approvers_{divisionId}";
            if (!_cache.TryGetValue(cacheKey, out List<string> approvers))
            {
                approvers = new List<string>();

                using (var conn = new SqlConnection(_connectionString))
                {
                    string query = @"
                        SELECT DISTINCT u.smallName AS ApName
                        FROM [DocumentControl].[dbo].[Users] u
                        WHERE u.Isvalid = 1
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
                                approvers.Add(reader["ApName"]?.ToString() ?? "");
                            }
                        }
                    }
                }

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                };
                _cache.Set(cacheKey, approvers, cacheOptions);
            }

            return approvers;
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

            _cache.Remove($"Approvers_{divisionId}");

            // Если у вас есть кэш по комбинациям отделов,
            // то можно пройтись по всем ключам в MemoryCache и удалить, 
            // где встречается divisionId.
        }
    }
}