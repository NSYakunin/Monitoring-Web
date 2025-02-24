using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Application.Services;
using Monitoring.Domain.Entities;
using Monitoring.Infrastructure.Services;
using System.Text.Json;

namespace Monitoring.UI.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IWorkRequestService _workRequestService;
        private readonly IWorkItemService _workItemService;
        private readonly INotificationService _notificationService;
        private readonly IUserSettingsService _userSettingsService;
        private readonly ILoginService _loginService;

        public IndexModel(
            IWorkItemService workItemService,
            INotificationService notificationService,
            IWorkRequestService workRequestService,
            IUserSettingsService userSettingsService,
            ILoginService loginService)
        {
            _workItemService = workItemService;
            _notificationService = notificationService;
            _workRequestService = workRequestService;
            _userSettingsService = userSettingsService;
            _loginService = loginService;
        }

        // Начало --- обычные свойства модели (как у вас было):
        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Executor { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }

        // Название текущего отдела (для интерфейса)
        public string DepartmentName { get; set; } = "Неизвестный отдел";

        // Текущее имя пользователя (smallName) из куки
        public string UserName { get; set; } = string.Empty;

        // Список всех отфильтрованных WorkItems
        public List<WorkItem> WorkItems { get; set; } = new List<WorkItem>();

        // Список исполнителей, загружаемый из БД для выбранного отдела
        public List<string> Executors { get; set; } = new List<string>();

        // Для экспорта: хранит порядок выбранных позиций (DocumentNumber)
        [BindProperty]
        public string SelectedItemsOrder { get; set; } = string.Empty;

        // Активные уведомления
        public List<Notification> Notifications { get; set; } = new List<Notification>();

        // Выбранный отдел (при фильтрации) - idDivision
        [BindProperty(SupportsGet = true)]
        public int? SelectedDivision { get; set; }

        // Список отделов, к которым пользователь имеет доступ
        public List<DivisionDto> AllowedDivisions { get; set; } = new();

        // Флаг: есть ли у пользователя доступ к настройкам
        public bool HasSettingsAccess { get; set; } = false;
        // Конец --- обычные свойства модели

        public async Task OnGet()
        {
            // 1) Проверяем куки (userName / divisionId)
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId") ||
                !HttpContext.Request.Cookies.ContainsKey("userName"))
            {
                Response.Redirect("/Login");
                return;
            }

            // 2) Считываем родной отдел (homeDivision) и userName
            int homeDivisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);
            UserName = HttpContext.Request.Cookies["userName"];

            // 3) Находим idUser
            int? userId = await _loginService.GetUserIdByNameAsync(UserName);
            if (userId == null)
            {
                Response.Redirect("/Login");
                return;
            }

            // 4) Проверяем, есть ли у пользователя галочка "Доступ к настройкам"
            HasSettingsAccess = await _userSettingsService.HasAccessToSettingsAsync(userId.Value);

            // 5) Загружаем список отделов, к которым есть доступ (UserAllowedDivisions)
            var userDivIds = await _userSettingsService.GetUserAllowedDivisionsAsync(userId.Value);
            if (userDivIds.Count == 0)
            {
                // Если нет записей, значит доступен только родной
                userDivIds.Add(homeDivisionId);
            }

            var allDivisions = await _userSettingsService.GetAllDivisionsAsync();
            AllowedDivisions = allDivisions
                .Where(d => userDivIds.Contains(d.IdDivision))
                .ToList();

            // 6) Определяем, какой отдел выбрать по умолчанию
            if (!SelectedDivision.HasValue)
            {
                // если родной отдел в списке – берем его
                if (AllowedDivisions.Any(d => d.IdDivision == homeDivisionId))
                {
                    SelectedDivision = homeDivisionId;
                }
                else if (AllowedDivisions.Count > 0)
                {
                    SelectedDivision = AllowedDivisions.First().IdDivision;
                }
            }

            // 7) Установка дат по умолчанию (если не заданы)
            if (!StartDate.HasValue)
                StartDate = new DateTime(2014, 1, 1);
            if (!EndDate.HasValue)
            {
                DateTime now = DateTime.Now;
                EndDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);
            }

            // 8) Деактивируем старые уведомления
            await _notificationService.DeactivateOldNotificationsAsync(90);

            // 9) Грузим уведомления по выбранному отделу (или родному)
            int divisionForNotifications = SelectedDivision ?? homeDivisionId;
            Notifications = await _notificationService.GetActiveNotificationsAsync(divisionForNotifications);

            // 10) Загружаем исполнителей и работы **по выбранному подразделению**:
            if (SelectedDivision.HasValue)
            {
                Executors = await _workItemService.GetExecutorsAsync(SelectedDivision.Value);
                WorkItems = await _workItemService.GetAllWorkItemsAsync(
                    new List<int> { SelectedDivision.Value }
                );
                DepartmentName = await _workItemService.GetDevAsync(SelectedDivision.Value);
            }
            else
            {
                Executors = new List<string>();
                WorkItems = new List<WorkItem>();
                DepartmentName = "Нет доступных подразделений";
            }

            // 11) Применяем фильтрацию
            ApplyFilters();

            // 12) Подсвечиваем строки, у которых есть Pending-заявки
            await HighlightRows();
        }

        /// <summary>
        /// AJAX-фильтр: вызывается, когда пользователь меняет даты, поиск, исполнителя или подразделение
        /// </summary>
        public async Task<IActionResult> OnGetFilterAsync(
            DateTime? startDate,
            DateTime? endDate,
            string? executor,
            string? searchQuery,
            int? selectedDivision)
        {
            // 1) Проверяем куки
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId") ||
                !HttpContext.Request.Cookies.ContainsKey("userName"))
            {
                return new JsonResult(new { error = "Нет нужных куки" });
            }

            // 2) userName, homeDivisionId
            UserName = HttpContext.Request.Cookies["userName"];
            int homeDivisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);

            // 3) Устанавливаем свойства модели, переданные в запросе
            StartDate = startDate ?? new DateTime(2014, 1, 1);
            EndDate = endDate ?? DateTime.Now;
            Executor = executor;
            SearchQuery = searchQuery;
            SelectedDivision = selectedDivision;

            // 4) Определяем доступные отделы (как в OnGet)
            int? userId = await _loginService.GetUserIdByNameAsync(UserName);
            if (userId == null)
                return new JsonResult(new { error = "Пользователь не найден" });

            var userDivIds = await _userSettingsService.GetUserAllowedDivisionsAsync(userId.Value);
            if (userDivIds.Count == 0)
            {
                userDivIds.Add(homeDivisionId);
            }
            var allDivisions = await _userSettingsService.GetAllDivisionsAsync();
            AllowedDivisions = allDivisions
                .Where(d => userDivIds.Contains(d.IdDivision))
                .ToList();

            // 5) Если переданного отдела нет в списке, берём что-то по умолчанию
            if (!SelectedDivision.HasValue ||
                !AllowedDivisions.Any(d => d.IdDivision == SelectedDivision.Value))
            {
                if (AllowedDivisions.Any(d => d.IdDivision == homeDivisionId))
                {
                    SelectedDivision = homeDivisionId;
                }
                else if (AllowedDivisions.Count > 0)
                {
                    SelectedDivision = AllowedDivisions.First().IdDivision;
                }
            }

            // 6) Загружаем исполнителей и WorkItems по выбранному отделу
            if (SelectedDivision.HasValue)
            {
                Executors = await _workItemService.GetExecutorsAsync(SelectedDivision.Value);
                WorkItems = await _workItemService.GetAllWorkItemsAsync(
                    new List<int> { SelectedDivision.Value }
                );
                DepartmentName = await _workItemService.GetDevAsync(SelectedDivision.Value);
            }
            else
            {
                Executors = new List<string>();
                WorkItems = new List<WorkItem>();
                DepartmentName = "Нет доступных подразделений";
            }

            // 7) Фильтрация
            ApplyFilters();

            // 8) Подсветка Pending-заявок
            await HighlightRows();

            // 9) Возвращаем partial с таблицей
            return Partial("_WorkItemsTablePartial", this);
        }

        /// <summary>
        /// Новый GET-метод, который возвращает список исполнителей в JSON для заданного отдела.
        /// Используется для динамического обновления выпадающего списка исполнителей
        /// при смене отделов (divisionSelect).
        /// </summary>
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnGetExecutorsAsync(int divisionId)
        {
            // Просто получаем список исполнителей для нужного отдела
            var executors = await _workItemService.GetExecutorsAsync(divisionId);
            return new JsonResult(executors);
        }

        /// <summary>
        /// Обработчик POST для создания заявки (корректировки/факт)
        /// </summary>
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostCreateRequestAsync()
        {
            try
            {
                // Читаем JSON-данные из тела запроса
                using var reader = new StreamReader(Request.Body);
                string body = await reader.ReadToEndAsync();
                var dto = JsonSerializer.Deserialize<CreateRequestDto>(body);
                if (dto == null)
                    return new JsonResult(new { success = false, message = "Невалидный JSON" });

                if (!HttpContext.Request.Cookies.ContainsKey("divisionId") ||
                    !HttpContext.Request.Cookies.ContainsKey("userName"))
                {
                    return new JsonResult(new { success = false, message = "No cookies." });
                }

                int homeDivisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);
                UserName = HttpContext.Request.Cookies["userName"];

                int actualDivisionId = SelectedDivision ?? homeDivisionId;
                Executors = await _workItemService.GetExecutorsAsync(actualDivisionId);
                WorkItems = await _workItemService.GetAllWorkItemsAsync(
                    new List<int> { actualDivisionId }
                );

                var witem = WorkItems.FirstOrDefault(x => x.DocumentNumber == dto.DocumentNumber);
                if (witem == null)
                    return new JsonResult(new { success = false, message = "WorkItem не найден" });

                // Проверяем, что текущий пользователь действительно исполнитель
                var allExecs = witem.Executor.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim()).ToList();
                if (!allExecs.Contains(UserName))
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Вы не являетесь исполнителем для данной работы."
                    });
                }

                // Создаём заявку
                var request = new WorkRequest
                {
                    WorkDocumentNumber = dto.DocumentNumber,
                    RequestType = dto.RequestType,
                    Sender = dto.Sender,
                    Receiver = dto.Receiver,
                    RequestDate = DateTime.Now,
                    ProposedDate = dto.ProposedDate,
                    Note = dto.Note,
                    Status = "Pending",
                    IsDone = false
                };
                await _workRequestService.CreateRequestAsync(request);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Принять/отклонить заявку
        /// </summary>
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostSetRequestStatusAsync()
        {
            UserName = HttpContext.Request.Cookies["userName"];
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = await reader.ReadToEndAsync();

                var data = JsonSerializer.Deserialize<StatusChangeDto>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "Невалидные данные" });

                // Проверяем, что текущий пользователь == Receiver
                var requestList = await _workRequestService.GetRequestsByDocumentNumberAsync(data.DocumentNumber);
                var req = requestList.FirstOrDefault(r => r.Id == data.RequestId);

                if (req == null)
                    return new JsonResult(new { success = false, message = "Заявка не найдена" });

                if (req.Receiver != UserName)
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "У вас нет прав на изменение статуса этой заявки."
                    });
                }

                // Меняем статус
                if (data.NewStatus != "Accepted" && data.NewStatus != "Declined")
                {
                    return new JsonResult(new { success = false, message = "Некорректный статус" });
                }
                await _workRequestService.SetRequestStatusAsync(data.RequestId, data.NewStatus);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Список моих входящих заявок (Pending)
        /// </summary>
        public async Task<IActionResult> OnGetMyRequestsAsync()
        {
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId") ||
                !HttpContext.Request.Cookies.ContainsKey("userName"))
            {
                return new JsonResult(new { success = false, message = "No cookies" });
            }

            string userName = HttpContext.Request.Cookies["userName"];
            UserName = userName;

            // Берём все заявки (изменённый метод GetAllRequestsAsync возвращает DocumentName и WorkName)
            var allRequests = await _workRequestService.GetAllRequestsAsync();

            // Оставляем только те, что адресованы этому пользователю, Pending, и не выполнены
            var myPending = allRequests
                .Where(r => r.Receiver == userName && r.Status == "Pending" && !r.IsDone)
                .ToList();

            // Расширяем анонимный объект выдачи полями docName и workName
            var result = myPending.Select(r => new {
                id = r.Id,
                workDocumentNumber = r.WorkDocumentNumber,
                requestType = r.RequestType,
                proposedDate = r.ProposedDate?.ToString("yyyy-MM-dd"),
                sender = r.Sender,
                note = r.Note,
                // Новые поля - берем из r.DocumentName и r.WorkName
                docName = r.DocumentName,
                workName = r.WorkName
            });
            return new JsonResult(result);
        }

        /// <summary>
        /// Сброс кэша
        /// </summary>
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostRefreshCacheAsync()
        {
            try
            {
                if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
                    return new JsonResult(new { success = false, message = "No division cookie" });

                int divisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);
                _workItemService.ClearCache(divisionId);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Logout
        /// </summary>
        public IActionResult OnGetLogout()
        {
            HttpContext.Response.Cookies.Delete("userName");
            HttpContext.Response.Cookies.Delete("divisionId");
            return RedirectToPage("Login");
        }

        /// <summary>
        /// Генерация PDF/Excel/Word (Сдаточный чек)
        /// </summary>
        public async Task<IActionResult> OnPostAsync()
        {
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId") ||
                !HttpContext.Request.Cookies.ContainsKey("userName"))
            {
                return RedirectToPage("/Login");
            }

            int homeDivisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);
            UserName = HttpContext.Request.Cookies["userName"];

            int actualDivisionId = SelectedDivision ?? homeDivisionId;
            Executors = await _workItemService.GetExecutorsAsync(actualDivisionId);
            WorkItems = await _workItemService.GetAllWorkItemsAsync(
                new List<int> { actualDivisionId }
            );

            string dev = await _workItemService.GetDevAsync(actualDivisionId);

            // Применяем те же фильтры
            ApplyFilters();

            // Смотрим, какие позиции выбраны (SelectedItemsOrder — JSON)
            if (!string.IsNullOrEmpty(SelectedItemsOrder))
            {
                var selectedDocs = JsonSerializer.Deserialize<List<string>>(SelectedItemsOrder);
                if (selectedDocs != null && selectedDocs.Count > 0)
                {
                    var filtered = WorkItems
                        .Where(w => selectedDocs.Contains(w.DocumentNumber))
                        .OrderBy(w => selectedDocs.IndexOf(w.DocumentNumber))
                        .ToList();

                    WorkItems = filtered;
                }
            }

            // Определяем формат (pdf, excel, word)
            string format = Request.Form["format"];
            if (format == "pdf")
            {
                var pdfBytes = ReportGenerator.GeneratePdf(
                    WorkItems,
                    $"Сдаточный чек от {DateTime.Now:dd.MM.yyyy}",
                    dev
                );
                return File(pdfBytes, "application/pdf", $"Чек_{DateTime.Now:yyyyMMdd}.pdf");
            }
            else if (format == "word")
            {
                var docBytes = ReportGeneratorWord.GenerateWord(
                    WorkItems,
                    $"Сдаточный чек от {DateTime.Now:dd.MM.yyyy}",
                    dev
                );
                return File(docBytes,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    $"Чек_{DateTime.Now:yyyyMMdd}.docx");
            }
            else if (format == "excel")
            {
                var xlsxBytes = ReportGeneratorExcel.GenerateExcel(
                    WorkItems,
                    $"Сдаточный чек от {DateTime.Now:dd.MM.yyyy}",
                    dev
                );
                return File(xlsxBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Чек_{DateTime.Now:yyyyMMdd}.xlsx");
            }

            // Если формат не распознан
            return Page();
        }

        /// <summary>
        /// Фильтрация WorkItems по дате, исполнителю, поиску
        /// </summary>
        private void ApplyFilters()
        {
            var query = WorkItems.AsQueryable();

            // Фильтр по исполнителю
            if (!string.IsNullOrEmpty(Executor))
            {
                query = query.Where(x =>
                    x.Executor != null &&
                    x.Executor.Contains(Executor, StringComparison.OrdinalIgnoreCase));
            }

            // Полнотекстовый поиск (DocumentName, WorkName, Executor, Controller, Approver)
            if (!string.IsNullOrEmpty(SearchQuery))
            {
                query = query.Where(x =>
                    (x.DocumentName != null && x.DocumentName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    || (x.WorkName != null && x.WorkName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    || (x.Executor != null && x.Executor.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    || (x.Controller != null && x.Controller.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    || (x.Approver != null && x.Approver.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                );
            }

            // Фильтр по дате "до EndDate"
            if (EndDate.HasValue)
            {
                query = query.Where(x =>
                    (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) <= EndDate);
            }

            WorkItems = query.ToList();
        }

        /// <summary>
        /// Подсветка строк, у которых есть Pending-заявки
        /// </summary>
        private async Task HighlightRows()
        {
            foreach (var item in WorkItems)
            {
                var requests = await _workRequestService.GetRequestsByDocumentNumberAsync(item.DocumentNumber);
                var pendingRequests = requests.Where(r => r.Status == "Pending" && !r.IsDone).ToList();

                if (pendingRequests.Any())
                {
                    bool hasFact = pendingRequests.Any(r => r.RequestType == "fact");
                    bool hasCorr = pendingRequests.Any(r => r.RequestType.StartsWith("корр"));

                    if (hasFact)
                        item.HighlightCssClass = "table-info";    // голубая подсветка
                    else if (hasCorr)
                        item.HighlightCssClass = "table-warning"; // желтая подсветка
                }
            }
        }
    }
}