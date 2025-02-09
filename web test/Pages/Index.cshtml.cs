using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using web_test.Services;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace web_test.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IWorkItemService _workItemService;

        // Получаем сервис через DI (конструктор)
        public IndexModel(IWorkItemService workItemService)
        {
            _workItemService = workItemService;
        }

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Executor { get; set; } // Раньше было executor (с маленькой)

        [BindProperty(SupportsGet = true)]
        public string SearchQuery { get; set; }

        public string DepartmentName { get; set; } = "Отдел №17";
        public string UserName { get; set; } = string.Empty;

        public List<WorkItem> WorkItems { get; set; } = new();
        public List<SelectListItem> Executors { get; set; } = new();

        public async Task<IActionResult> OnGet()
        {
            // Проверяем куки divisionId
            if (!HttpContext.Request.Cookies.TryGetValue("divisionId", out var divisionIdString)
                || !int.TryParse(divisionIdString, out int divisionId))
            {
                return RedirectToPage("/Login");
            }

            UserName = HttpContext.Request.Cookies["userName"];
            DepartmentName = $"Отдел №{divisionId}";

            // Устанавливаем значения по умолчанию для дат
            if (!StartDate.HasValue)
                StartDate = new DateTime(2014, 1, 1);

            if (!EndDate.HasValue)
            {
                DateTime now = DateTime.Now;
                EndDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);
            }

            // Загружаем исполнителей
            Executors = await _workItemService.GetExecutorsAsync(divisionId);

            // Загружаем (и фильтруем) данные
            WorkItems = await _workItemService.GetWorkItemsAsync(
                divisionId,
                StartDate.Value,
                EndDate.Value,
                Executor,
                SearchQuery
            );

            return Page();
        }

        // Метод для AJAX-фильтра
        public async Task<IActionResult> OnGetFilterAsync(string executor, DateTime? startDate, DateTime? endDate, string search)
        {
            // Снова читаем divisionId из кук
            if (!HttpContext.Request.Cookies.TryGetValue("divisionId", out var divisionIdString)
                || !int.TryParse(divisionIdString, out int divisionId))
            {
                return BadRequest("Невалидная информация о подразделении");
            }

            // Запоминаем параметры в свойства модели (Executor, StartDate и т.д.)
            this.Executor = executor;
            this.StartDate = startDate;
            this.EndDate = endDate;
            this.SearchQuery = search;

            // Задаём дефолтные даты, если не переданы
            if (!StartDate.HasValue)
                StartDate = new DateTime(2014, 1, 1);

            if (!EndDate.HasValue)
            {
                var now = DateTime.Now;
                EndDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);
            }

            // Загружаем исполнителей
            Executors = await _workItemService.GetExecutorsAsync(divisionId);

            // Загружаем данные
            WorkItems = await _workItemService.GetWorkItemsAsync(
                divisionId,
                StartDate.Value,
                EndDate.Value,
                Executor,
                SearchQuery
            );

            // Возвращаем partial, чтобы обновилась таблица
            return Partial("_WorkItemsTablePartial", this);
        }


        /// <summary>
        /// Выход пользователя (очистка кук).
        /// </summary>
        public IActionResult OnGetLogout()
        {
            HttpContext.Response.Cookies.Delete("userName");
            HttpContext.Response.Cookies.Delete("divisionId");
            return RedirectToPage("Login");
        }

        /// <summary>
        /// Загрузка и фильтрация данных для таблицы (с учётом выбранных параметров).
        /// </summary>
        //private async Task LoadDataAsync(int divisionId)
        //{
        //    string connectionString = "Data Source=ASCON;Initial Catalog=DocumentControl;Persist Security Info=False;User ID=test;Password=test123456789";

        //    // Преобразуем даты в строки подходящего формата
        //    string start = StartDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "2014-01-01 00:00:00";
        //    string end = EndDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        //    string query = @"
        //        SELECT d.Number, wu.idWork,
        //            td.Name + ' ' + d.Name AS DocumentName,
        //            w.Name AS WorkName,
        //            u.smallName AS Executor,
        //            (SELECT smallName FROM Users WHERE idUser = wucontr.idUser) AS Controller,
        //            (SELECT smallName FROM Users WHERE idUser = wuc.idUser) AS Approver,
        //            w.DatePlan,
        //            wu.DateKorrect1,
        //            wu.DateKorrect2,
        //            wu.DateKorrect3,
        //            w.DateFact
        //        FROM WorkUser wu
        //            INNER JOIN Works w ON wu.idWork = w.id
        //            INNER JOIN Documents d ON w.idDocuments = d.id
        //            LEFT JOIN WorkUserCheck wuc ON wuc.idWork = w.id
        //            LEFT JOIN WorkUserControl wucontr ON wucontr.idWork = w.id
        //            INNER JOIN TypeDocs td ON td.id = d.idTypeDoc
        //            INNER JOIN Users u ON wu.idUser = u.idUser
        //        WHERE
        //            wu.dateFact IS NULL
        //            AND wu.idUser IN (SELECT idUser FROM Users WHERE idDivision = @divId)
        //            AND w.datePlan BETWEEN @start AND @end
        //    ";

        //    // Дополнительная фильтрация по исполнителю (если задан)
        //    if (!string.IsNullOrEmpty(executor))
        //    {
        //        query += " AND u.smallName = @executor ";
        //    }

        //    // Дополнительная фильтрация по строке поиска (если задана).
        //    // Ищем и по названию документа (td.Name + d.Name), и по названию работы (w.Name).
        //    if (!string.IsNullOrEmpty(SearchQuery))
        //    {
        //        query += " AND (td.Name + ' ' + d.Name LIKE '%' + @search + '%' OR w.Name LIKE '%' + @search + '%') ";
        //    }

        //    // Сортировка по номеру документа (тут, судя по фрагменту, была определённая логика сортировки)
        //    query += @"
        //        ORDER BY
        //            SUBSTRING(d.Number, 5, 2),
        //            SUBSTRING(d.Number, 3, 2),
        //            SUBSTRING(d.Number, 1, 2);
        //    ";

        //    var workItemsDict = new Dictionary<string, WorkItem>();

        //    using (var conn = new SqlConnection(connectionString))
        //    using (var cmd = new SqlCommand(query, conn))
        //    {
        //        cmd.Parameters.AddWithValue("@start", start);
        //        cmd.Parameters.AddWithValue("@end", end);
        //        cmd.Parameters.AddWithValue("@divId", divisionId);

        //        if (!string.IsNullOrEmpty(executor))
        //            cmd.Parameters.AddWithValue("@executor", executor);

        //        if (!string.IsNullOrEmpty(SearchQuery))
        //            cmd.Parameters.AddWithValue("@search", SearchQuery);

        //        await conn.OpenAsync();
        //        using (var reader = await cmd.ExecuteReaderAsync())
        //        {
        //            while (await reader.ReadAsync())
        //            {
        //                string documentNumber = reader["Number"]?.ToString() + '/' + reader["idWork"]?.ToString();
        //                string documentName = reader["DocumentName"]?.ToString();
        //                string workName = reader["WorkName"]?.ToString();
        //                string currentExec = reader["Executor"]?.ToString();
        //                string controller = reader["Controller"]?.ToString();
        //                string approver = reader["Approver"]?.ToString();
        //                DateTime? planDate = reader["DatePlan"] as DateTime?;
        //                DateTime? kor1 = reader["DateKorrect1"] as DateTime?;
        //                DateTime? kor2 = reader["DateKorrect2"] as DateTime?;
        //                DateTime? kor3 = reader["DateKorrect3"] as DateTime?;
        //                DateTime? factDate = reader["DateFact"] as DateTime?;


        //                //// Дополнительная логика – например, если дата плановая > EndDate, пропускаем, но это на ваше усмотрение
        //                //if ((planDate.HasValue && planDate > EndDate) ||
        //                //    (kor1.HasValue && kor1 > EndDate) ||
        //                //    (kor2.HasValue && kor2 > EndDate) ||
        //                //    (kor3.HasValue && kor3 > EndDate))
        //                //{
        //                //    continue;
        //                //}

        //                // Ключ, чтобы объединять в одну строку записи, у которых совпадают остальные данные, но разные исполнители
        //                string key = $"{documentName}|{workName}|{controller}|{approver}|{planDate}|{kor1}|{kor2}|{kor3}|{factDate}";
        //                if (!workItemsDict.ContainsKey(key))
        //                {
        //                    workItemsDict[key] = new WorkItem
        //                    {
        //                        DocumentNumber = documentNumber,
        //                        DocumentName = documentName,
        //                        WorkName = workName,
        //                        Executor = currentExec,
        //                        Controller = controller,
        //                        Approver = approver,
        //                        PlanDate = planDate,
        //                        Korrect1 = kor1,
        //                        Korrect2 = kor2,
        //                        Korrect3 = kor3,
        //                        FactDate = factDate,
        //                        FactChoiseTime = EndDate
        //                    };
        //                }
        //                else
        //                {
        //                    var existing = workItemsDict[key];
        //                    var executorList = existing.Executor
        //                        .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
        //                        .ToList();

        //                    if (!string.IsNullOrEmpty(currentExec) && !executorList.Contains(currentExec))
        //                    {
        //                        executorList.Add(currentExec);
        //                        existing.Executor = string.Join(", ", executorList);
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    // Преобразуем результат в список WorkItem и сохраняем в свойство PageModel
        //    WorkItems = workItemsDict.Values.ToList();
        //    _allWorkItems = WorkItems;
        //}

        /// <summary>
        /// Загрузка уникальных исполнителей для выпадающего списка.
        /// </summary>
        //private async Task LoadExecutorsAsync(int divisionId)
        //{
        //    Executors.Clear();

        //    string connectionString = "Data Source=ASCON;Initial Catalog=DocumentControl;Persist Security Info=False;User ID=test;Password=test123456789";
        //    string query = @"
        //        SELECT DISTINCT u.smallName AS ExecName
        //        FROM Users u
        //        WHERE u.idDivision = @divId
        //        ORDER BY u.smallName
        //    ";

        //    using (var conn = new SqlConnection(connectionString))
        //    using (var cmd = new SqlCommand(query, conn))
        //    {
        //        cmd.Parameters.AddWithValue("@divId", divisionId);
        //        await conn.OpenAsync();
        //        using (var reader = await cmd.ExecuteReaderAsync())
        //        {
        //            while (await reader.ReadAsync())
        //            {
        //                string executorName = reader["ExecName"]?.ToString();
        //                if (!string.IsNullOrEmpty(executorName))
        //                {
        //                    Executors.Add(new SelectListItem
        //                    {
        //                        Value = executorName,
        //                        Text = executorName
        //                    });
        //                }
        //            }
        //        }
        //    }
        //}


        // Метод POST: Генерируем PDF
        // (важно: делаем async, чтобы дождаться LoadDataAsync)
        public async Task<IActionResult> OnPostAsync()
        {
            if (!HttpContext.Request.Cookies.TryGetValue("divisionId", out var divisionIdString)
                || !int.TryParse(divisionIdString, out int divisionId))
            {
                return RedirectToPage("/Login");
            }

            // Снова загружаем данные (по текущим фильтрам, которые пришли через hidden поля)
            if (!StartDate.HasValue)
                StartDate = new DateTime(2014, 1, 1);
            if (!EndDate.HasValue)
            {
                var now = DateTime.Now;
                EndDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);
            }

            WorkItems = await _workItemService.GetWorkItemsAsync(
                divisionId,
                StartDate.Value,
                EndDate.Value,
                Executor,
                SearchQuery
            );

            // Генерация PDF
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "report.pdf");
            ReportGenerator.GeneratePdf(this.WorkItems, "Мой отчет");

            // Проверяем, существует ли файл
            if (System.IO.File.Exists(filePath))
            {
                // Отдаём файл пользователю (браузер скачает)
                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, "application/pdf", "report.pdf");
            }
            else
            {
                // Можно добавить сообщение об ошибке, но для упрощения:
                return Page();
            }
        }
    }
}