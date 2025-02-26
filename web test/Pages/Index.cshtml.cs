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

        // НОВО: Фильтр по "Принимающему"
        [BindProperty(SupportsGet = true)]
        public string? Approver { get; set; }

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

        // НОВО: список "Принимающих"
        public List<string> Approvers { get; set; } = new List<string>();

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

        // Флаг: есть ли у пользователя доступ к отправке заявок на закрытие (перенос) работ
        public bool HasSendCloseRequestAccess { get; set; } = false;
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

            // 4.2) Проверяем, есть ли у пользователя доступ к отправке заявок на закрытие (перенос) работ
            HasSendCloseRequestAccess = await _userSettingsService.HasAccessToSendCloseRequestAsync(userId.Value);

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
                Approvers = await _workItemService.GetApproversAsync(SelectedDivision.Value);
                WorkItems = await _workItemService.GetAllWorkItemsAsync(
                    new List<int> { SelectedDivision.Value }
                );
                DepartmentName = await _workItemService.GetDevAsync(SelectedDivision.Value);
            }
            else
            {
                Executors = new List<string>();
                Approvers = new List<string>();
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
            string? approver,
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
            Approver = approver;
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
                Approvers = await _workItemService.GetApproversAsync(SelectedDivision.Value);
                WorkItems = await _workItemService.GetAllWorkItemsAsync(
                    new List<int> { SelectedDivision.Value }
                );
                DepartmentName = await _workItemService.GetDevAsync(SelectedDivision.Value);
            }
            else
            {
                Executors = new List<string>();
                Approvers = new List<string>();
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
        /// AJAX: получить список Approvers для division
        /// </summary>
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnGetApproversAsync(int divisionId)
        {
            var approvers = await _workItemService.GetApproversAsync(divisionId);
            return new JsonResult(approvers);
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
                // --------------------------------------------------
        // 1) Создание заявки (POST) 
        //    (корр, факт-закрытие и т.д.)
        // --------------------------------------------------
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostCreateRequestAsync()
        {
            try
            {
                // Читаем DTO из тела:
                using var reader = new StreamReader(Request.Body);
                string body = await reader.ReadToEndAsync();
                var dto = JsonSerializer.Deserialize<CreateRequestDto>(body);
                if (dto == null)
                    return new JsonResult(new { success = false, message = "Невалидный JSON" });

                // Проверяем куки
                if (!HttpContext.Request.Cookies.ContainsKey("divisionId") ||
                    !HttpContext.Request.Cookies.ContainsKey("userName"))
                {
                    return new JsonResult(new { success = false, message = "No cookies." });
                }

                int homeDivisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);
                UserName = HttpContext.Request.Cookies["userName"];
                int actualDivisionId = SelectedDivision ?? homeDivisionId;

                // 1) Находим соответствующий WorkItem
                var allItems = await _workItemService.GetAllWorkItemsAsync(
                    new List<int> { actualDivisionId }
                );
                var witem = allItems.FirstOrDefault(x => x.DocumentNumber == dto.DocumentNumber);
                if (witem == null)
                {
                    return new JsonResult(new { success = false, message = "WorkItem не найден" });
                }

                // 2) Проверяем, что текущий пользователь действительно в списке исполнителей
                var execs = witem.Executor
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .ToList();
                if (!execs.Contains(UserName))
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Вы не являетесь исполнителем для данной работы."
                    });
                }

                // 3) Создаём новую заявку, полностью заполняя все поля из WorkItem
                var newRequest = new WorkRequest
                {
                    WorkDocumentNumber = witem.DocumentNumber,
                    DocumentName = witem.DocumentName, // "ТипДок + название"
                    WorkName = witem.WorkName,      // Если нужно отдельно
                    RequestType = dto.RequestType,
                    Sender = UserName,   // либо UserName, если надо
                    Receiver = dto.Receiver,
                    RequestDate = DateTime.Now,
                    IsDone = false,
                    Note = dto.Note,
                    ProposedDate = dto.ProposedDate,
                    Status = "Pending",

                    // Копируем поля из WorkItem
                    Executor = witem.Executor,
                    Controller = witem.Controller,
                    PlanDate = witem.PlanDate,
                    Korrect1 = witem.Korrect1,
                    Korrect2 = witem.Korrect2,
                    Korrect3 = witem.Korrect3
                };

                await _workRequestService.CreateRequestAsync(newRequest);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Обновление существующей заявки (если она ещё Pending и пользователь = Sender)
        /// </summary>
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostUpdateRequestAsync()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = await reader.ReadToEndAsync();
                var dto = JsonSerializer.Deserialize<UpdateRequestDto>(body);
                if (dto == null)
                    return new JsonResult(new { success = false, message = "Невалидные данные" });

                if (!HttpContext.Request.Cookies.ContainsKey("userName"))
                {
                    return new JsonResult(new { success = false, message = "No cookies" });
                }
                UserName = HttpContext.Request.Cookies["userName"];

                // Находим заявку
                var requests = await _workRequestService.GetRequestsByDocumentNumberAsync(dto.DocumentNumber);
                var req = requests.FirstOrDefault(r => r.Id == dto.Id);
                if (req == null)
                    return new JsonResult(new { success = false, message = "Заявка не найдена" });

                if (req.Sender != UserName)
                {
                    return new JsonResult(new { success = false, message = "Вы не являетесь автором заявки" });
                }

                if (req.Status != "Pending")
                {
                    return new JsonResult(new { success = false, message = "Заявка уже обработана" });
                }

                // Обновляем
                req.RequestType = dto.RequestType;
                req.Receiver = dto.Receiver;
                req.ProposedDate = dto.ProposedDate;
                req.Note = dto.Note;

                await _workRequestService.UpdateRequestAsync(req);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Удаление заявки (если Pending и пользователь = Sender)
        /// </summary>
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostDeleteRequestAsync()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<DeleteRequestDto>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "Невалидные данные" });

                if (!HttpContext.Request.Cookies.ContainsKey("userName"))
                {
                    return new JsonResult(new { success = false, message = "No cookies" });
                }
                UserName = HttpContext.Request.Cookies["userName"];

                var allReq = await _workRequestService.GetRequestsByDocumentNumberAsync(data.DocumentNumber);
                var req = allReq.FirstOrDefault(x => x.Id == data.RequestId);
                if (req == null)
                    return new JsonResult(new { success = false, message = "Заявка не найдена" });

                if (req.Sender.ToString() != UserName)
                    return new JsonResult(new { success = false, message = "Вы не автор этой заявки" });

                if (req.Status != "Pending")
                    return new JsonResult(new { success = false, message = "Нельзя удалить обработанную заявку" });

                await _workRequestService.DeleteRequestAsync(req.Id);

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

                // Находим заявку
                var allForDoc = await _workRequestService.GetRequestsByDocumentNumberAsync(data.DocumentNumber);
                var req = allForDoc.FirstOrDefault(r => r.Id == data.RequestId);
                if (req == null)
                    return new JsonResult(new { success = false, message = "Заявка не найдена" });

                // Проверяем, что текущий пользователь == Receiver
                if (req.Receiver != UserName)
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "У вас нет прав на изменение статуса этой заявки."
                    });
                }

                // Меняем статус (Accepted/Declined)
                if (data.NewStatus != "Accepted" && data.NewStatus != "Declined")
                    return new JsonResult(new { success = false, message = "Некорректный статус" });

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
            if (!HttpContext.Request.Cookies.ContainsKey("userName"))
            {
                return new JsonResult(new { success = false, message = "No cookies" });
            }

            string userName = HttpContext.Request.Cookies["userName"];

            // Берём PENDING заявки из таблицы Requests
            var myPending = await _workRequestService.GetPendingRequestsByReceiverAsync(userName);

            // Преобразуем в JSON-форму для таблицы "Мои входящие заявки"
            var result = myPending.Select(r => new
            {
                id = r.Id,
                workDocumentNumber = r.WorkDocumentNumber,
                requestType = r.RequestType,
                proposedDate = r.ProposedDate?.ToString("yyyy-MM-dd"),
                sender = r.Sender,
                note = r.Note,

                // Для колонок в таблице:
                documentName = r.DocumentName, // "Документ"
                workName = r.WorkName,     // "Работа"
                executor = r.Executor,
                controller = r.Controller,
                approver = r.Receiver,     // "Принимающий"
                planDate = r.PlanDate?.ToString("yyyy-MM-dd"),
                korrect1 = r.Korrect1?.ToString("yyyy-MM-dd"),
                korrect2 = r.Korrect2?.ToString("yyyy-MM-dd"),
                korrect3 = r.Korrect3?.ToString("yyyy-MM-dd")
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
            Approvers = await _workItemService.GetApproversAsync(actualDivisionId);
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

            // НОВО: Фильтр по принимающему
            if (!string.IsNullOrEmpty(Approver))
            {
                query = query.Where(x => x.Approver != null &&
                                         x.Approver.Contains(Approver, StringComparison.OrdinalIgnoreCase));
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
        /// Подсветка строк + заполнение данных о заявке (если Pending, Sender=CurrentUser)
        /// </summary>
        private async Task HighlightRows()
        {
            foreach (var item in WorkItems)
            {
                var requests = await _workRequestService.GetRequestsByDocumentNumberAsync(item.DocumentNumber);
                // Ищем PENDING запрос от текущего пользователя-исполнителя (Sender=UserName)
                var pendingFromMe = requests.FirstOrDefault(r => r.Status == "Pending" && !r.IsDone && r.Executor == UserName);
                if (pendingFromMe != null)
                {
                    if (pendingFromMe.RequestType == "факт")
                        item.HighlightCssClass = "table-info";
                    else if (pendingFromMe.RequestType.StartsWith("корр"))
                        item.HighlightCssClass = "table-warning";

                    // Заполним, чтобы отобразить в data-атрибутах
                    item.UserPendingRequestId = pendingFromMe.Id;
                    item.UserPendingRequestType = pendingFromMe.RequestType;
                    item.UserPendingProposedDate = pendingFromMe.ProposedDate;
                    item.UserPendingRequestNote = pendingFromMe.Note;
                    item.UserPendingReceiver = pendingFromMe.Receiver;
                }
            }
        }
    }
}