using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace web_test.Pages
{
    public class IndexModel : PageModel
    {
        // �������� ��� �������� ��� (�������� �� URL ��� GET-�������)
        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        // ��� ������� �� ����������� (�������� �� URL) 
        [BindProperty(SupportsGet = true)]
        public string executor { get; set; }  // ��������� �������� �� ����������� ������

        // �������� ������������� � ������� ������������
        public string DepartmentName { get; set; } = "����� �17";
        public string UserName { get; set; } = string.Empty;

        // ������ ������ ��� �������� �������
        public List<WorkItem> WorkItems { get; set; } = new List<WorkItem>();

        // ������ ������������ ��� ���������� ����������� ������
        public List<SelectListItem> Executors { get; set; } = new List<SelectListItem>();

        public async Task OnGet()
        {
            // ��������� ������� ����������� ���
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
            {
                Response.Redirect("/Login");
                return;
            }

            var divisionIdString = HttpContext.Request.Cookies["divisionId"];
            if (!int.TryParse(divisionIdString, out int divisionId))
            {
                Response.Redirect("/Login");
                return;
            }

            // ������ �� ���� ��� ������������
            UserName = HttpContext.Request.Cookies["userName"];
            DepartmentName = $"����� �{divisionId}";

            // ���� ���� �� ������, ����� �������� �� ���������
            if (!StartDate.HasValue) StartDate = new DateTime(2014, 1, 1);
            // �������� � EndDate ��������� ���� �������� ������
            DateTime now = DateTime.Now;
            if (!EndDate.HasValue) EndDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);

            // �������� ������ ���������� ������������ ��� ����������� ������
            await LoadExecutorsAsync(divisionId);

            // ��������� ������ ��� �������, � ������ ������� �� ����������� (���� ������)
            await LoadDataAsync(divisionId);
        }

        /// <summary>
        /// ����� ��� ������� ��� � ������ ������������.
        /// </summary>
        public IActionResult OnGetLogout()
        {
            HttpContext.Response.Cookies.Delete("userName");
            HttpContext.Response.Cookies.Delete("divisionId");
            return RedirectToPage("Login");
        }

        /// <summary>
        /// �������� ������ ��� ������� � ������������ ������������
        /// (���� � ����� ������ ��������� ������������, ������� � ����� ������).
        /// </summary>
        private async Task LoadDataAsync(int divisionId)
        {
            // ������ ����������� � ����������� ��� ���� ��
            string connectionString = "Data Source=ASCON;Initial Catalog=DocumentControl;Persist Security Info=False;User ID=test;Password=test123456789";

            // ����������� ���� ��� SQL-�������
            string start = StartDate.Value.ToString("yyyy-MM-dd HH:mm:ss");
            string end = EndDate.Value.ToString("yyyy-MM-dd HH:mm:ss");

            // ������� ������
            // �������� ��������, ��� ����� wu.dateFact IS NULL (��� �������), ��������, � ����� ������ ����� ��-�������
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
                    AND wu.idUser IN (SELECT idUser FROM Users WHERE idDivision = @divId)
                    AND w.datePlan BETWEEN @start AND @end
            ";

            // ���� ������ �����������, ��������� �������
            if (!string.IsNullOrEmpty(executor))
            {
                query += " AND u.smallName = @executor ";
            }

            // �������������� ���������� (������, ����� ��������)
            query += @"
                ORDER BY
                    SUBSTRING(Number, 5, 2),
                    SUBSTRING(Number, 3, 2),
                    SUBSTRING(Number, 1, 2);
            ";

            // ������� ��� �����������. ���� � ���������� ���������� �����,
            // � �������� � ������ WorkItem � ���������� �������������.
            var workItemsDict = new Dictionary<string, WorkItem>();

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@start", start);
                cmd.Parameters.AddWithValue("@end", end);
                cmd.Parameters.AddWithValue("@divId", divisionId);

                if (!string.IsNullOrEmpty(executor))
                {
                    cmd.Parameters.AddWithValue("@executor", executor);
                }

                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        // ��������� ������
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

                        // ����������, ���� ���� ��� ������������� ������� �� ������� EndDate (������ ������)
                        if ((planDate.HasValue && planDate > EndDate) ||
                            (kor1.HasValue && kor1 > EndDate) ||
                            (kor2.HasValue && kor2 > EndDate) ||
                            (kor3.HasValue && kor3 > EndDate))
                        {
                            continue;
                        }

                        // ��������� ���� ��� �����������
                        string key = $"{documentName}|{workName}|{controller}|{approver}|{planDate}|{kor1}|{kor2}|{kor3}|{factDate}";

                        // ���� ������ ��� ��� � ������� � ������
                        if (!workItemsDict.ContainsKey(key))
                        {
                            workItemsDict[key] = new WorkItem
                            {
                                DocumentName = documentName,
                                WorkName = workName,
                                Executor = currentExec, // ����� ������� �����������
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
                            // ���� ������ ��� ����������, ��������� ������ �����������, ���� ��� ���
                            var existing = workItemsDict[key];
                            var executorList = existing.Executor.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList();
                            // ���������, ���� �� ��� ����� �����������
                            if (!string.IsNullOrEmpty(currentExec) && !executorList.Contains(currentExec))
                            {
                                executorList.Add(currentExec);
                                existing.Executor = string.Join(", ", executorList);
                            }
                        }
                    }
                }
            }

            // ������������� ��������������� ������ � �������� ������
            WorkItems = workItemsDict.Values.ToList();
        }

        /// <summary>
        /// �������� ���������� ������������ ��� ����������� ������.
        /// </summary>

        public async Task<IActionResult> OnGetFilterAsync(string executor, DateTime? startDate, DateTime? endDate)
        {
            // ������������� �������� ��������
            this.executor = executor;
            StartDate = startDate;
            EndDate = endDate;

            // ��������� ������ ��� �������
            await LoadDataAsync(int.Parse(HttpContext.Request.Cookies["divisionId"]));

            // ���������� Partial View � ����������� ��������
            return Partial("_WorkItemsTablePartial", this);
        }

        private async Task LoadExecutorsAsync(int divisionId)
        {
            string connectionString = "Data Source=ASCON;Initial Catalog=DocumentControl;Persist Security Info=False;User ID=test;Password=test123456789";
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
                            Executors.Add(new SelectListItem
                            {
                                Value = executorName,
                                Text = executorName
                            });
                        }
                    }
                }
            }
        }
    }
}