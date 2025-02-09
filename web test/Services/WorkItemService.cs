using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;

namespace web_test.Services
{
    public class WorkItemService : IWorkItemService
    {
        private readonly IConfiguration _configuration;

        // Получаем IConfiguration через DI (в неё можно сложить ConnectionString в appsettings.json)
        public WorkItemService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<List<SelectListItem>> GetExecutorsAsync(int divisionId)
        {
            var result = new List<SelectListItem>();
            var connectionString = _configuration.GetConnectionString("DocumentControlDb");

            string query = @"
                SELECT DISTINCT u.smallName AS ExecName
                FROM Users u
                WHERE u.idDivision = @divId
                ORDER BY u.smallName
            ";

            using var conn = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@divId", divisionId);
            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string executorName = reader["ExecName"]?.ToString();
                if (!string.IsNullOrEmpty(executorName))
                {
                    result.Add(new SelectListItem
                    {
                        Value = executorName,
                        Text = executorName
                    });
                }
            }

            return result;
        }

        public async Task<List<WorkItem>> GetWorkItemsAsync(
            int divisionId,
            DateTime startDate,
            DateTime endDate,
            string executor,
            string search)
        {
            var workItemsDict = new Dictionary<string, WorkItem>();

            var connectionString = _configuration.GetConnectionString("DocumentControlDb");

            // Пример запроса. Выносим логику фильтрации в SQL (добавляем AND ... при необходимости).
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
                    AND w.datePlan BETWEEN @startDate AND @endDate
            ";

            // Добавляем условия в зависимости от параметров
            if (!string.IsNullOrEmpty(executor))
            {
                query += " AND u.smallName = @executor ";
            }

            if (!string.IsNullOrEmpty(search))
            {
                query += " AND (td.Name + ' ' + d.Name LIKE '%' + @search + '%' OR w.Name LIKE '%' + @search + '%') ";
            }

            // Сортировка (примерно повторяем логику)
            query += @"
                ORDER BY
                    SUBSTRING(d.Number, 5, 2),
                    SUBSTRING(d.Number, 3, 2),
                    SUBSTRING(d.Number, 1, 2);
            ";

            using var conn = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(query, conn);

            cmd.Parameters.AddWithValue("@divId", divisionId);
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);

            if (!string.IsNullOrEmpty(executor))
                cmd.Parameters.AddWithValue("@executor", executor);

            if (!string.IsNullOrEmpty(search))
                cmd.Parameters.AddWithValue("@search", search);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
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

                // Ключ, чтобы объединять исполнителей в одну запись
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
                    // Если ключ уже есть, значит добавляем исполнителя к уже имеющемуся
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

            return workItemsDict.Values.ToList();
        }
    }
}
