// Services/WorkItemService.cs
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace web_test.Services
{
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
            const string cacheKey = "AllWorkItems";
            if (!_cache.TryGetValue(cacheKey, out List<WorkItem> workItems))
            {
                workItems = await LoadWorkItemsFromDatabaseAsync(divisionId);
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(_configuration.GetValue<int>("CacheSettings:CacheDurationInMinutes")));
                _cache.Set(cacheKey, workItems, cacheEntryOptions);
            }
            return workItems;
        }

        public async Task<List<SelectListItem>> GetExecutorsAsync(int divisionId)
        {
            const string cacheKey = "Executors";
            if (!_cache.TryGetValue(cacheKey, out List<SelectListItem> executors))
            {
                executors = await LoadExecutorsFromDatabaseAsync(divisionId);
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(_configuration.GetValue<int>("CacheSettings:CacheDurationInMinutes")));
                _cache.Set(cacheKey, executors, cacheEntryOptions);
            }
            return executors;
        }

        private async Task<List<WorkItem>> LoadWorkItemsFromDatabaseAsync(int divisionId)
        {
            var workItems = new List<WorkItem>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            string query = @"
                SELECT d.Number, wu.idWork,
                    td.Name + ' ' + d.Name AS DocumentName,
                    w.Name AS WorkName,
                    u.smallName AS Executor,
                    (SELECT smallName FROM Users WHERE idUser = wucontr.idUser) AS Controller,
                    (SELECT smallName FROM Users WHERE idUser = wuc.idUser) AS Approver,
                    w.DatePlan,
                    wu.DateKorrect1,
                    wu.DateKorrect2,
                    wu.DateKorrect3,
                    w.DateFact
                FROM WorkUser wu
                    INNER JOIN Works w ON wu.idWork = w.id
                    INNER JOIN Documents d ON w.idDocuments = d.id
                    LEFT JOIN WorkUserCheck wuc ON wuc.idWork = w.id
                    LEFT JOIN WorkUserControl wucontr ON wucontr.idWork = w.id
                    INNER JOIN TypeDocs td ON td.id = d.idTypeDoc
                    INNER JOIN Users u ON wu.idUser = u.idUser
                WHERE
                    wu.dateFact IS NULL
                    AND wu.idUser IN (SELECT idUser FROM Users WHERE idDivision = @divId)
            ";

            var workItemsDict = new Dictionary<string, WorkItem>();

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@divId", divisionId);

                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string documentNumber = reader["Number"]?.ToString() + '/' + reader["idWork"]?.ToString();
                        string documentName = reader["DocumentName"]?.ToString();
                        string workName = reader["WorkName"]?.ToString();
                        string currentExec = reader["Executor"]?.ToString();
                        string controller = reader["Controller"]?.ToString();
                        string approver = reader["Approver"]?.ToString();
                        DateTime? planDate = reader["DatePlan"] as DateTime?;
                        DateTime? kor1 = reader["DateKorrect1"] as DateTime?;
                        DateTime? kor2 = reader["DateKorrect2"] as DateTime?;
                        DateTime? kor3 = reader["DateKorrect3"] as DateTime?;
                        DateTime? factDate = reader["DateFact"] as DateTime?;

                        // Ключ, чтобы объединять в одну строку записи, у которых совпадают остальные данные, но разные исполнители
                        string key = $"{documentName}|{workName}|{controller}|{approver}|{planDate}|{kor1}|{kor2}|{kor3}|{factDate}";
                        if (!workItemsDict.ContainsKey(key))
                        {
                            workItemsDict[key] = new WorkItem
                            {
                                DocumentNumber = documentNumber,
                                DocumentName = documentName,
                                WorkName = workName,
                                Executor = currentExec,
                                Controller = controller,
                                Approver = approver,
                                PlanDate = planDate,
                                Korrect1 = kor1,
                                Korrect2 = kor2,
                                Korrect3 = kor3,
                                FactDate = factDate
                            };
                        }
                        else
                        {
                            var existing = workItemsDict[key];
                            var executorList = existing.Executor
                                .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
                                .ToList();

                            if (!string.IsNullOrEmpty(currentExec) && !executorList.Contains(currentExec))
                            {
                                executorList.Add(currentExec);
                                existing.Executor = string.Join(", ", executorList);
                            }
                        }
                    }
                }
            }

            // Преобразуем результат в список WorkItem и сохраняем в свойство PageModel
            workItems = workItemsDict.Values.ToList();
            return workItems;
        }

        private async Task<List<SelectListItem>> LoadExecutorsFromDatabaseAsync(int divisionId)
        {
            var executors = new List<SelectListItem>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            string query = @"
                SELECT DISTINCT u.smallName AS ExecName
                FROM Users u
                WHERE u.idDivision = @divId
                ORDER BY u.smallName
            ";

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@divId", divisionId);
                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string executorName = reader["ExecName"]?.ToString();
                        if (!string.IsNullOrEmpty(executorName))
                        {
                            executors.Add(new SelectListItem
                            {
                                Value = executorName,
                                Text = executorName
                            });
                        }
                    }
                }
            }
            return executors;
        }
    }
}