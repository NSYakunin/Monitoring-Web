using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace web_test.Pages
{
    public class IndexModel : PageModel
    {
        // Публичные свойства для биндинга дат
        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        // Пример: текущее название подразделения (заглушка)
        public string DepartmentName { get; set; } = "Отдел №17";

        // Список данных для таблицы
        public List<WorkItem> WorkItems { get; set; } = new List<WorkItem>();

        public async Task OnGet()
        {
            // Если даты не заданы, можно выставить их в какие-то разумные границы
            if (!StartDate.HasValue) StartDate = new DateTime(2014, 1, 1);
            if (!EndDate.HasValue) EndDate = new DateTime(2025, 1, 31, 8, 11, 31);

            await LoadDataAsync();
        }

        public async Task OnPost()
        {
            // При отправке формы (если сделаете <form method="post">) 
            // данные из BindProperty уже будут в StartDate и EndDate
            await LoadDataAsync();
        }

        [Obsolete]
        private async Task LoadDataAsync()
        {
            // Пример подключения к БД (обязательно поменяйте строку подключения!)
            string connectionString = "Data Source = ASCON; Initial Catalog = DocumentControl; Persist Security Info = False; User ID = test;Password = test123456789";

            // Формируем даты в нужном формате. Например, в стиле 'yyyy-MM-dd HH:mm:ss'
            string start = StartDate.Value.ToString("yyyy-MM-dd HH:mm:ss");
            string end = EndDate.Value.ToString("yyyy-MM-dd HH:mm:ss");

            // SQL-запрос с параметрами, чтобы избежать SQL-инъекций
            string query = @"
                SELECT 
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
                    AND wu.idUser IN (SELECT idUser FROM Users WHERE idDivision = 17)
                    AND w.datePlan BETWEEN @start AND @end
                ORDER BY
                    SUBSTRING(Number, 5, 2),
                    SUBSTRING(Number, 3, 2),
                    SUBSTRING(Number, 1, 2);";

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@start", start);
                cmd.Parameters.AddWithValue("@end", end);

                await conn.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    WorkItems.Clear();
                    while (await reader.ReadAsync())
                    {
                        var item = new WorkItem
                        {
                            DocumentName = reader["DocumentName"]?.ToString(),
                            WorkName = reader["WorkName"]?.ToString(),
                            Executor = reader["Executor"]?.ToString(),
                            Controller = reader["Controller"]?.ToString(),
                            Approver = reader["Approver"]?.ToString(),
                            PlanDate = reader["DatePlan"] as DateTime?,
                            Korrect1 = reader["DateKorrect1"] as DateTime?,
                            Korrect2 = reader["DateKorrect2"] as DateTime?,
                            Korrect3 = reader["DateKorrect3"] as DateTime?,
                            FactDate = reader["DateFact"] as DateTime?
                        };
                        WorkItems.Add(item);
                    }
                }
            }
        }
    }
}
