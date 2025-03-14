using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data;
using System.Data.SqlClient;

namespace Monitoring.UI.Pages
{
    public class PerformanceModel : PageModel
    {
        // Параметры, приходящие из формы
        [BindProperty(SupportsGet = true)]
        public DateTime StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime EndDate { get; set; }

        // Результаты для вывода
        public List<PerfResultDto> Results { get; set; } = new();

        // Задаём строку подключения или пользуемся сервисом (лучше через DI):
        private readonly IConfiguration _configuration;

        public PerformanceModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet()
        {
            // Если даты не заданы, то берём "с 1-го числа текущего месяца по сегодня"
            if (StartDate == default)
                StartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (EndDate == default)
                EndDate = DateTime.Now.Date;

            Results = LoadPerformanceData(StartDate, EndDate);
        }

        private List<PerfResultDto> LoadPerformanceData(DateTime start, DateTime end)
        {
            List<PerfResultDto> data = new();

            // Простейший пример запроса:
            //  - Считаем "план" как кол-во работ, у которых w.DatePlan в диапазоне
            //  - Считаем "факт" как кол-во работ, у которых wu.DateFact в диапазоне
            //  - Группируем по подразделению исполнителя (u.idDivision)
            //  - Исключаем какие-то типы документов, если надо (doc.idTypeDoc<>15), и т.п.
            //  - При необходимости отбрасываем "dateFact is null" и т.д. – зависит от вашей логики

            string sql = @"
                SELECT 
                    d.idDivision AS DivisionId,
                    d.smallNameDivision AS DivisionName,
                    SUM(CASE WHEN w.DatePlan >= @start AND w.DatePlan <= @end THEN 1 ELSE 0 END) AS PlanCount,
                    SUM(CASE WHEN wu.DateFact >= @start AND wu.DateFact <= @end THEN 1 ELSE 0 END) AS FactCount
                FROM WorkUser wu
                    INNER JOIN Works w ON wu.idWork = w.id
                    INNER JOIN Documents doc ON w.idDocuments = doc.id
                    INNER JOIN Users u ON wu.idUser = u.idUser
                    INNER JOIN Divisions d ON u.idDivision = d.idDivision
                WHERE 1=1
                  AND doc.idTypeDoc <> 15            -- исключить определённый тип документа, если надо
                GROUP BY d.idDivision, d.smallNameDivision
                ORDER BY d.idDivision;
                ";

            string connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@start", start);
                cmd.Parameters.AddWithValue("@end", end);

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new PerfResultDto
                        {
                            DivisionId = reader.GetInt32(0),
                            DivisionName = reader.GetString(1),
                            PlanCount = reader.GetInt32(2),
                            FactCount = reader.GetInt32(3)
                        };
                        data.Add(row);
                    }
                }
            }

            return data;
        }
    }

    // Простой DTO для результата
    public class PerfResultDto
    {
        public int DivisionId { get; set; }
        public string DivisionName { get; set; } = "";
        public int PlanCount { get; set; }
        public int FactCount { get; set; }
    }
}