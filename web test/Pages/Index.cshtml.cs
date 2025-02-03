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
        // Свойства для биндинга дат (приходят из URL при GET-запросе)
        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        // Для фильтра по исполнителю (приходит из URL) 
        [BindProperty(SupportsGet = true)]
        public string executor { get; set; }  // Выбранное значение из выпадающего списка

        // Название подразделения и текущий пользователь
        public string DepartmentName { get; set; } = "Отдел №17";
        public string UserName { get; set; } = string.Empty;

        // Список данных для основной таблицы
        public List<WorkItem> WorkItems { get; set; } = new List<WorkItem>();

        // Список исполнителей для заполнения выпадающего списка
        public List<SelectListItem> Executors { get; set; } = new List<SelectListItem>();

        public async Task OnGet()
        {
            // Проверяем наличие необходимых кук
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

            // Читаем из куки имя пользователя
            UserName = HttpContext.Request.Cookies["userName"];
            DepartmentName = $"Отдел №{divisionId}";

            // Если даты не заданы, задаём значения по умолчанию
            if (!StartDate.HasValue) StartDate = new DateTime(2014, 1, 1);
            // Получаем в EndDate последний день текущего месяца
            DateTime now = DateTime.Now;
            if (!EndDate.HasValue) EndDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);

            // Получаем список уникальных исполнителей для выпадающего списка
            await LoadExecutorsAsync(divisionId);

            // Загружаем данные для таблицы, с учётом фильтра по исполнителю (если выбран)
            await LoadDataAsync(divisionId);
        }

        /// <summary>
        /// Метод для очистки кук и выхода пользователя.
        /// </summary>
        public IActionResult OnGetLogout()
        {
            HttpContext.Response.Cookies.Delete("userName");
            HttpContext.Response.Cookies.Delete("divisionId");
            return RedirectToPage("Login");
        }

        /// <summary>
        /// Загрузка данных для таблицы с группировкой исполнителей
        /// (если у одной работы несколько исполнителей, выводим в одной ячейке).
        /// </summary>
        private async Task LoadDataAsync(int divisionId)
        {
            // Строка подключения – адаптируйте под вашу БД
            string connectionString = "Data Source=ASCON;Initial Catalog=DocumentControl;Persist Security Info=False;User ID=test;Password=test123456789";

            // Форматируем даты для SQL-запроса
            string start = StartDate.Value.ToString("yyyy-MM-dd HH:mm:ss");
            string end = EndDate.Value.ToString("yyyy-MM-dd HH:mm:ss");

            // Базовый запрос
            // Обратите внимание, что здесь wu.dateFact IS NULL (как условие), возможно, в вашем случае нужно по-другому
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

            // Если выбран исполнитель, добавляем условие
            if (!string.IsNullOrEmpty(executor))
            {
                query += " AND u.smallName = @executor ";
            }

            // Дополнительная сортировка (пример, можно изменить)
            query += @"
                ORDER BY
                    SUBSTRING(Number, 5, 2),
                    SUBSTRING(Number, 3, 2),
                    SUBSTRING(Number, 1, 2);
            ";

            // Словарь для группировки. Ключ – уникальная комбинация полей,
            // а значение – объект WorkItem с собранными исполнителями.
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
                        // Считываем данные
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

                        // Пропускаем, если план или корректировки выходят за границы EndDate (пример логики)
                        if ((planDate.HasValue && planDate > EndDate) ||
                            (kor1.HasValue && kor1 > EndDate) ||
                            (kor2.HasValue && kor2 > EndDate) ||
                            (kor3.HasValue && kor3 > EndDate))
                        {
                            continue;
                        }

                        // Формируем ключ для группировки
                        string key = $"{documentName}|{workName}|{controller}|{approver}|{planDate}|{kor1}|{kor2}|{kor3}|{factDate}";

                        // Если записи ещё нет в словаре – создаём
                        if (!workItemsDict.ContainsKey(key))
                        {
                            workItemsDict[key] = new WorkItem
                            {
                                DocumentName = documentName,
                                WorkName = workName,
                                Executor = currentExec, // Пишем первого исполнителя
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
                            // Если запись уже существует, добавляем нового исполнителя, если его нет
                            var existing = workItemsDict[key];
                            var executorList = existing.Executor.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList();
                            // Проверяем, есть ли уже такой исполнитель
                            if (!string.IsNullOrEmpty(currentExec) && !executorList.Contains(currentExec))
                            {
                                executorList.Add(currentExec);
                                existing.Executor = string.Join(", ", executorList);
                            }
                        }
                    }
                }
            }

            // Перекладываем сгруппированные данные в итоговый список
            WorkItems = workItemsDict.Values.ToList();
        }

        /// <summary>
        /// Загрузка уникальных исполнителей для выпадающего списка.
        /// </summary>

        public async Task<IActionResult> OnGetFilterAsync(string executor, DateTime? startDate, DateTime? endDate)
        {
            // Устанавливаем значения фильтров
            this.executor = executor;
            StartDate = startDate;
            EndDate = endDate;

            // Загружаем данные для таблицы
            await LoadDataAsync(int.Parse(HttpContext.Request.Cookies["divisionId"]));

            // Возвращаем Partial View с обновленной таблицей
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