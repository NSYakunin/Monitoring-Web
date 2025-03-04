﻿using DocumentFormat.OpenXml.Spreadsheet;
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

        // --- Свойства модели --- //
        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Executor { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Approver { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }

        // Название текущего отдела (для интерфейса)
        public string DepartmentName { get; set; } = "Неизвестный отдел";
        public string UserName { get; set; } = string.Empty;

        public List<WorkItem> WorkItems { get; set; } = new List<WorkItem>();

        public List<string> Executors { get; set; } = new List<string>();
        public List<string> Approvers { get; set; } = new List<string>();

        // Для экспорта: хранит порядок выбранных позиций (DocumentNumber)
        [BindProperty]
        public string SelectedItemsOrder { get; set; } = string.Empty;

        public List<Notification> Notifications { get; set; } = new List<Notification>();

        [BindProperty(SupportsGet = true)]
        public int? SelectedDivision { get; set; }

        // Список отделов, к которым пользователь имеет доступ
        public List<DivisionDto> AllowedDivisions { get; set; } = new();


        // Флаг: есть ли доступ к настройкам
        public bool HasSettingsAccess { get; set; } = false;

        // 4.3) Флаг: есть ли у пользователя хотя бы одна входящая Pending-заявка
        public bool HasPendingRequests { get; set; } = false;

        // 4.3) Флаг: есть ли доступ к ЗАКРЫТИЮ работ (canCloseWork)
        public bool HasCloseWorkAccess { get; set; } = false;

        // Флаг: есть ли доступ к отправке заявок
        public bool HasSendCloseRequestAccess { get; set; } = false;

        // --- End of properties ---

        public async Task OnGet()
        {
            // 1) Проверяем куки
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

            // 4.3) Проверяем, есть ли у пользователя право **закрывать** работы
            HasCloseWorkAccess = await _userSettingsService.HasAccessToCloseWorkAsync(userId.Value);
            if (HasCloseWorkAccess)
            {
                // Если есть — тогда проверяем, есть ли у этого пользователя входящие "Pending" заявки
                var myPending = await _workRequestService.GetPendingRequestsByReceiverAsync(UserName);
                HasPendingRequests = (myPending != null && myPending.Count > 0);
            }

            // 5) Загружаем список отделов, к которым есть доступ
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
                if (AllowedDivisions.Any(d => d.IdDivision == homeDivisionId))
                {
                    SelectedDivision = homeDivisionId;
                }
                else if (AllowedDivisions.Count > 0)
                {
                    SelectedDivision = AllowedDivisions.First().IdDivision;
                }
            }

            // 7) Дефолтные даты
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

            // 10) Загружаем исполнителей и работы
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

            // 11) Применяем фильтры
            ApplyFilters();

            // 12) Подсвечиваем строки (Pending-заявки)
            await HighlightRows();
        }

        /// <summary>
        /// AJAX-фильтр: вызывается при изменении дат, исполнителя, принимающего, подразделения и т.д.
        /// Возвращаем partial.
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

            // 3) Устанавливаем свойства
            StartDate = startDate ?? new DateTime(2014, 1, 1);
            EndDate = endDate ?? DateTime.Now;
            Executor = executor;
            Approver = approver;
            SearchQuery = searchQuery;
            SelectedDivision = selectedDivision;

            // 4) Определяем доступные отделы
            int? userId = await _loginService.GetUserIdByNameAsync(UserName);
            if (userId == null)
                return new JsonResult(new { error = "Пользователь не найден" });

            // ВАЖНО: перезаполняем флаги доступа (иначе кнопка заявки будет отключена)
            HasSettingsAccess = await _userSettingsService.HasAccessToSettingsAsync(userId.Value);
            HasSendCloseRequestAccess = await _userSettingsService.HasAccessToSendCloseRequestAsync(userId.Value);

            var userDivIds = await _userSettingsService.GetUserAllowedDivisionsAsync(userId.Value);
            if (userDivIds.Count == 0)
            {
                userDivIds.Add(homeDivisionId);
            }
            var allDivisions = await _userSettingsService.GetAllDivisionsAsync();
            AllowedDivisions = allDivisions
                .Where(d => userDivIds.Contains(d.IdDivision))
                .ToList();

            // 5) Если переданный отдел не в списке, берём что-то по умолчанию
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

            // 6) Загружаем исполнителей и WorkItems
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

            // 9) Возвращаем partial
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
        /// AJAX: получить список исполнителей для division
        /// </summary>
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnGetExecutorsAsync(int divisionId)
        {
            var executors = await _workItemService.GetExecutorsAsync(divisionId);
            return new JsonResult(executors);
        }

        // --------------------------------------------------
        // 1) Создание заявки (POST)
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

                // 1) Находим WorkItem
                var allItems = await _workItemService.GetAllWorkItemsAsync(
                    new List<int> { actualDivisionId }
                );
                var witem = allItems.FirstOrDefault(x => x.DocumentNumber == dto.DocumentNumber);
                if (witem == null)
                {
                    return new JsonResult(new { success = false, message = "WorkItem не найден" });
                }

                // 2) Проверяем, что текущий пользователь - исполнитель
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

                // 3) Создаём новую заявку
                var newRequest = new WorkRequest
                {
                    WorkDocumentNumber = witem.DocumentNumber,
                    DocumentName = witem.DocumentName, // "ТипДок + название"
                    WorkName = witem.WorkName,
                    RequestType = dto.RequestType,
                    Sender = UserName,
                    Receiver = dto.Receiver,
                    RequestDate = DateTime.Now,
                    IsDone = false,
                    Note = dto.Note,
                    ProposedDate = dto.ProposedDate,
                    Status = "Pending",

                    Executor = witem.Executor,
                    Controller = witem.Controller,
                    PlanDate = witem.PlanDate,
                    Korrect1 = witem.Korrect1,
                    Korrect2 = witem.Korrect2,
                    Korrect3 = witem.Korrect3
                };

                // ВАЖНО: возвращаем ID записи
                int newRequestId = await _workRequestService.CreateRequestAsync(newRequest);

                return new JsonResult(new { success = true, requestId = newRequestId });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Обновление (POST) заявки
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

                // Ищем заявку
                var requests = await _workRequestService.GetRequestsByDocumentNumberAsync(dto.DocumentNumber);
                var req = requests.FirstOrDefault(r => r.Id == dto.Id);
                if (req == null)
                    return new JsonResult(new { success = false, message = "Заявка не найдена" });

                if (req.Sender != UserName)
                {
                    return new JsonResult(new { success = false, message = "Вы не автор заявки" });
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
        /// Удаление заявки (POST)
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

                if (req.Sender != UserName)
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
        /// Принять/отклонить заявку (POST)
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

            var myPending = await _workRequestService.GetPendingRequestsByReceiverAsync(userName);

            var result = myPending.Select(r => new
            {
                id = r.Id,
                workDocumentNumber = r.WorkDocumentNumber,
                requestType = r.RequestType,
                proposedDate = r.ProposedDate?.ToString("yyyy-MM-dd"),
                sender = r.Sender,
                note = r.Note,

                documentName = r.DocumentName,
                workName = r.WorkName,
                executor = r.Executor,
                controller = r.Controller,
                approver = r.Receiver,
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
        /// Экспорт PDF/Excel/Word (Сдаточный чек)
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

            // Фильтр
            ApplyFilters();

            // Выбранные позиции
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

            // Определяем формат
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

            return Page();
        }

        /// <summary>
        /// Фильтрация WorkItems
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

            // Фильтр по принимающему
            if (!string.IsNullOrEmpty(Approver))
            {
                query = query.Where(x =>
                    x.Approver != null &&
                    x.Approver.Contains(Approver, StringComparison.OrdinalIgnoreCase));
            }

            // Поиск (DocumentName, WorkName, Executor, Controller, Approver)
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

            // Фильтр по дате <= EndDate
            if (EndDate.HasValue)
            {
                query = query.Where(x =>
                    (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) <= EndDate.Value);
            }

            WorkItems = query.ToList();
        }

        /// <summary>
        /// Подсветка строк, если есть Pending-заявка от текущего пользователя
        /// </summary>
        private async Task HighlightRows()
        {
            foreach (var item in WorkItems)
            {
                var requests = await _workRequestService.GetRequestsByDocumentNumberAsync(item.DocumentNumber);

                // Ищем PENDING запрос от текущего пользователя
                var pendingFromMe = requests.FirstOrDefault(r =>
                    r.Status == "Pending" && !r.IsDone && r.Sender == UserName);

                if (pendingFromMe != null)
                {
                    if (pendingFromMe.RequestType == "факт")
                        item.HighlightCssClass = "table-info";
                    else if (pendingFromMe.RequestType.StartsWith("корр"))
                        item.HighlightCssClass = "table-warning";

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