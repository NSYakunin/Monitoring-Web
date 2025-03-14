using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data;
using System.Data.SqlClient;

namespace Monitoring.UI.Pages
{
    public class PerformanceModel : PageModel
    {
        // ���������, ���������� �� �����
        [BindProperty(SupportsGet = true)]
        public DateTime StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime EndDate { get; set; }

        // ���������� ��� ������
        public List<PerfResultDto> Results { get; set; } = new();

        // ����� ������ ����������� ��� ���������� �������� (����� ����� DI):
        private readonly IConfiguration _configuration;

        public PerformanceModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet()
        {
            // ���� ���� �� ������, �� ���� "� 1-�� ����� �������� ������ �� �������"
            if (StartDate == default)
                StartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (EndDate == default)
                EndDate = DateTime.Now.Date;

            Results = LoadPerformanceData(StartDate, EndDate);
        }

        private List<PerfResultDto> LoadPerformanceData(DateTime start, DateTime end)
        {
            List<PerfResultDto> data = new();

            // ���������� ������ �������:
            //  - ������� "����" ��� ���-�� �����, � ������� w.DatePlan � ���������
            //  - ������� "����" ��� ���-�� �����, � ������� wu.DateFact � ���������
            //  - ���������� �� ������������� ����������� (u.idDivision)
            //  - ��������� �����-�� ���� ����������, ���� ���� (doc.idTypeDoc<>15), � �.�.
            //  - ��� ������������� ����������� "dateFact is null" � �.�. � ������� �� ����� ������

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
                  AND doc.idTypeDoc <> 15            -- ��������� ����������� ��� ���������, ���� ����
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

    // ������� DTO ��� ����������
    public class PerfResultDto
    {
        public int DivisionId { get; set; }
        public string DivisionName { get; set; } = "";
        public int PlanCount { get; set; }
        public int FactCount { get; set; }
    }
}