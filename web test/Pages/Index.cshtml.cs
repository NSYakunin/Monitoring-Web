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

        public IndexModel(IWorkItemService workItemService,
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

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Executor { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }

        public string DepartmentName { get; set; } = "Отдел №17";
        public string UserName { get; set; } = string.Empty;

        public List<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
        public List<string> Executors { get; set; } = new List<string>();

        [BindProperty]
        public string SelectedItemsOrder { get; set; } = string.Empty;

        public List<Notification> Notifications { get; set; } = new List<Notification>();

        [BindProperty(SupportsGet = true)]
        public int? SelectedDivision { get; set; } // выбранный отдел (для фильтров)

        // Список отделов, к которым пользователь имеет доступ:
        public List<DivisionDto> AllowedDivisions { get; set; } = new();

        // Показывает, есть ли у пользователя доступ к настройкам
        public bool HasSettingsAccess { get; set; } = false;

        public async Task OnGet()
        {
            // 0) Проверка куки
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
            {
                Response.Redirect("/Login");
                return;
            }

            // Читаем "родной" отдел из куки + userName
            int homeDivisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);
            UserName = HttpContext.Request.Cookies["userName"];

            // 1) Получаем idUser по userName 
            int? userId = await _loginService.GetUserIdByNameAsync(UserName);
            if (userId == null)
            {
                // Если такого пользователя нет, редиректим на логин
                Response.Redirect("/Login");
                return;
            }

            // 2) Проверяем право на настройки
            HasSettingsAccess = await _userSettingsService.HasAccessToSettingsAsync(userId.Value);

            // 3) Получаем список всех "разрешённых" отделов (UserAllowedDivisions):
            var userDivIds = await _userSettingsService.GetUserAllowedDivisionsAsync(userId.Value);

            // 4) Если у пользователя "нет" записей в UserAllowedDivisions,
            //    значит он может видеть только свой "родной" отдел (homeDivisionId).
            if (userDivIds.Count == 0)
            {
                userDivIds.Add(homeDivisionId);
            }

            // 5) Извлекаем из таблицы Divisions только те, что в userDivIds
            var allDivisions = await _userSettingsService.GetAllDivisionsAsync();
            AllowedDivisions = allDivisions
                .Where(d => userDivIds.Contains(d.IdDivision))
                .ToList();

            // 6) Определяем SelectedDivision:
            //    - если пользователь не выбрал руками (SelectedDivision == null)
            //      то ставим по умолчанию "родной" отдел, если он входит в AllowedDivisions,
            //      иначе — первый доступный из списка AllowedDivisions.
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
                // если вообще нет AllowedDivisions, SelectedDivision останется null
            }

            // Установка дат по умолчанию
            if (!StartDate.HasValue)
                StartDate = new DateTime(2014, 1, 1);

            DateTime now = DateTime.Now;
            if (!EndDate.HasValue)
                EndDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);

            // Деактивируем старые уведомления (например, > 90 дней)
            await _notificationService.DeactivateOldNotificationsAsync(90);

            // Определяем, по какому отделу грузить уведомления
            int divisionForNotifications = SelectedDivision ?? homeDivisionId;
            Notifications = await _notificationService.GetActiveNotificationsAsync(divisionForNotifications);

            // Определяем "актуальный" отдел, по которому будем грузить исполнителей и работы
            int actualDivisionId = SelectedDivision ?? homeDivisionId;

            // Загружаем список исполнителей для "актуального" подразделения
            Executors = await _workItemService.GetExecutorsAsync(actualDivisionId);

            // Загружаем WorkItems именно для этого же подразделения
            WorkItems = await _workItemService.GetAllWorkItemsAsync(
                new List<int> { actualDivisionId }
            );

            // Считываем название отдела (smallNameDivision) тоже для него
            DepartmentName = await _workItemService.GetDevAsync(actualDivisionId);

            // Применяем фильтры (даты, поиск, исполнитель)
            ApplyFilters();

            // Подсвечиваем строки с незавершёнными заявками (если есть)
            HighlightRows();
        }

        /// <summary>
        /// AJAX-обработчик, вызываемый при изменении фильтров (startDate, endDate, executor, searchQuery, selectedDivision).
        /// Возвращает Partial с таблицей.
        /// </summary>
        public async Task<IActionResult> OnGetFilterAsync(
            DateTime? startDate,
            DateTime? endDate,
            string? executor,
            string? searchQuery,
            int? selectedDivision // <-- добавили параметр для подразделения
        )
        {
            // 1) Проверяем куки
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
                return new JsonResult(new { error = "Не найдены куки divisionId" });

            // 2) Считываем userName из куки (можно проверить и userId)
            UserName = HttpContext.Request.Cookies["userName"];
            int homeDivisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);

            // 3) Устанавливаем поля модели
            StartDate = startDate ?? new DateTime(2014, 1, 1);
            EndDate = endDate ?? DateTime.Now;
            Executor = executor;
            SearchQuery = searchQuery;
            SelectedDivision = selectedDivision;

            // 4) Нужно заново определить AllowedDivisions
            int? userId = await _loginService.GetUserIdByNameAsync(UserName);
            if (userId == null)
            {
                return new JsonResult(new { error = "Пользователь не найден" });
            }
            var userDivIds = await _userSettingsService.GetUserAllowedDivisionsAsync(userId.Value);
            if (userDivIds.Count == 0)
            {
                userDivIds.Add(homeDivisionId);
            }
            var allDivisions = await _userSettingsService.GetAllDivisionsAsync();
            AllowedDivisions = allDivisions
                .Where(d => userDivIds.Contains(d.IdDivision))
                .ToList();

            // Если SelectedDivision не задан (или недопустим), берём первый из AllowedDivisions
            if (!SelectedDivision.HasValue || !AllowedDivisions.Any(d => d.IdDivision == SelectedDivision.Value))
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

            int actualDivisionId = SelectedDivision ?? homeDivisionId;

            // 5) Загружаем исполнителей и работы для выбранного подразделения
            Executors = await _workItemService.GetExecutorsAsync(actualDivisionId);
            WorkItems = await _workItemService.GetAllWorkItemsAsync(new List<int> { actualDivisionId });

            // Название отдела (smallNameDivision)
            DepartmentName = await _workItemService.GetDevAsync(actualDivisionId);

            // 6) Применяем фильтры и подсветку
            ApplyFilters();
            HighlightRows();

            // 7) Возвращаем partial (HTML-фрагмент) с таблицей
            return Partial("_WorkItemsTablePartial", this);
        }

        /// <summary>
        /// Хендлер для создания заявки (POST)
        /// </summary>
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostCreateRequestAsync()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = await reader.ReadToEndAsync();

                var dto = JsonSerializer.Deserialize<CreateRequestDto>(body);
                if (dto == null)
                    return new JsonResult(new { success = false, message = "Невалидный JSON" });

                // Проверка пользователя
                if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
                    return new JsonResult(new { success = false, message = "No division cookie." });

                int homeDivisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);
                UserName = HttpContext.Request.Cookies["userName"];

                // При POST тут SelectedDivision может не прийти,
                // но если нужно, можем брать из "предыдущей" логики.
                int actualDivisionId = SelectedDivision ?? homeDivisionId;

                // Загружаем список исполнителей и WorkItems для нужного division
                Executors = await _workItemService.GetExecutorsAsync(actualDivisionId);
                WorkItems = await _workItemService.GetAllWorkItemsAsync(
                    new List<int> { actualDivisionId }
                );

                DepartmentName = await _workItemService.GetDevAsync(actualDivisionId);

                // Ищем нужный WorkItem по DocumentNumber
                var witem = WorkItems.FirstOrDefault(x => x.DocumentNumber == dto.DocumentNumber);
                if (witem == null)
                {
                    return new JsonResult(new { success = false, message = "WorkItem не найден" });
                }

                // Если в witem.Executor несколько имён через запятую, проверяем
                var allExecutors = witem.Executor.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .ToList();

                if (!allExecutors.Contains(UserName))
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Вы не являетесь исполнителем, запрос недоступен."
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
        /// Хендлер для принятия/отклонения заявки (POST)
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

                // Надо проверить, что текущий пользователь == Receiver
                var requestList = await _workRequestService.GetRequestsByDocumentNumberAsync(data.DocumentNumber);
                var req = requestList.FirstOrDefault(r => r.Id == data.RequestId);

                if (req == null)
                    return new JsonResult(new { success = false, message = "Заявка не найдена" });

                if (req.Receiver != UserName)
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "У вас нет прав для принятия/отклонения этой заявки."
                    });
                }

                // Устанавливаем статус
                string newStatus = data.NewStatus; // "Accepted" или "Declined"
                if (newStatus != "Accepted" && newStatus != "Declined")
                {
                    return new JsonResult(new { success = false, message = "Некорректный статус" });
                }

                await _workRequestService.SetRequestStatusAsync(data.RequestId, newStatus);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// AJAX-хендлер: возвращает "мои входящие заявки" (Pending) в JSON
        /// </summary>
        public async Task<IActionResult> OnGetMyRequestsAsync()
        {
            // 1) Проверяем, что пользователь зашёл
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
            {
                return new JsonResult(new { success = false, message = "Не найдены куки divisionId" });
            }

            // 2) Считываем userName
            string userName = HttpContext.Request.Cookies["userName"];
            UserName = userName;

            // 3) Загружаем все заявки и фильтруем, где Receiver == userName и Status == "Pending" и !IsDone
            var allRequests = await _workRequestService.GetAllRequestsAsync();
            var myPendingRequests = allRequests
                .Where(r => r.Receiver == userName && r.Status == "Pending" && !r.IsDone)
                .ToList();

            // 4) Упрощённо возвращаем анонимный список
            var result = myPendingRequests.Select(r => new {
                id = r.Id,
                workDocumentNumber = r.WorkDocumentNumber,
                requestType = r.RequestType,
                proposedDate = r.ProposedDate?.ToString("yyyy-MM-dd"),
                sender = r.Sender,
                note = r.Note
            });

            return new JsonResult(result);
        }

        /// <summary>
        /// POST: сброс кэша
        /// </summary>
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> OnPostRefreshCacheAsync()
        {
            try
            {
                // Проверка
                if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
                    return new JsonResult(new { success = false, message = "No division cookie." });

                int divisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);

                // Сбрасываем кэш
                _workItemService.ClearCache(divisionId);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Обработчик логаута (GET)
        /// </summary>
        public IActionResult OnGetLogout()
        {
            // Логаут
            HttpContext.Response.Cookies.Delete("userName");
            HttpContext.Response.Cookies.Delete("divisionId");
            return RedirectToPage("Login");
        }

        /// <summary>
        /// Обработчик POST для формирования PDF/Excel/Word (Сдаточный чек).
        /// </summary>
        public async Task<IActionResult> OnPostAsync()
        {
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
                return RedirectToPage("/Login");

            int homeDivisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);
            UserName = HttpContext.Request.Cookies["userName"];

            // Используем SelectedDivision, если оно задано и доступно
            int actualDivisionId = SelectedDivision ?? homeDivisionId;

            // Загружаем исполнителей и WorkItems для одного отдела:
            Executors = await _workItemService.GetExecutorsAsync(actualDivisionId);
            WorkItems = await _workItemService.GetAllWorkItemsAsync(
                new List<int> { actualDivisionId }
            );

            string dev = await _workItemService.GetDevAsync(actualDivisionId);

            // Применяем фильтры
            ApplyFilters();

            // Смотрим выбранные позиции (JSON-список DocumentNumber)
            if (!string.IsNullOrEmpty(SelectedItemsOrder))
            {
                var selectedList = JsonSerializer.Deserialize<List<string>>(SelectedItemsOrder);
                if (selectedList != null && selectedList.Count > 0)
                {
                    // Оставим WorkItems, DocumentNumber которых в selectedList
                    var filtered = WorkItems
                        .Where(w => selectedList.Contains(w.DocumentNumber))
                        .ToList();

                    // Сортируем по порядку из selectedList
                    filtered = filtered
                        .OrderBy(w => selectedList.IndexOf(w.DocumentNumber))
                        .ToList();

                    WorkItems = filtered;
                }
            }

            // Определяем формат (pdf, word, excel)
            string format = Request.Form["format"];
            if (format == "pdf")
            {
                var pdfBytes = ReportGenerator.GeneratePdf(
                    WorkItems,
                    $"Сдаточный чек от {DateTime.Now.ToShortDateString()}",
                    dev
                );

                return File(pdfBytes, "application/pdf", $"Чек от {DateTime.Now:dd.MM.yyyy}.pdf");
            }
            else if (format == "word")
            {
                var docBytes = ReportGeneratorWord.GenerateWord(
                    WorkItems,
                    $"Сдаточный чек от {DateTime.Now.ToShortDateString()}",
                    dev
                );

                return File(
                    docBytes,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    $"Чек от {DateTime.Now:dd.MM.yyyy}.docx"
                );
            }
            else if (format == "excel")
            {
                var xlsxBytes = ReportGeneratorExcel.GenerateExcel(
                    WorkItems,
                    $"Сдаточный чек от {DateTime.Now.ToShortDateString()}",
                    dev
                );

                return File(
                    xlsxBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Чек от {DateTime.Now:dd.MM.yyyy}.xlsx"
                );
            }

            // Если формат не определён, просто возвращаем страницу
            return Page();
        }

        /// <summary>
        /// Применение фильтров (Executor, SearchQuery, EndDate) к WorkItems
        /// </summary>
        private void ApplyFilters()
        {
            var filtered = WorkItems.AsQueryable();

            if (!string.IsNullOrEmpty(Executor))
            {
                filtered = filtered.Where(x =>
                    x.Executor != null &&
                    x.Executor.Contains(Executor, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(SearchQuery))
            {
                filtered = filtered.Where(x =>
                    (x.DocumentName != null && x.DocumentName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    || (x.WorkName != null && x.WorkName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    || (x.Executor != null && x.Executor.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    || (x.Controller != null && x.Controller.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    || (x.Approver != null && x.Approver.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                );
            }

            // Фильтр по дате (конкретно EndDate)
            if (EndDate.HasValue)
            {
                filtered = filtered.Where(x =>
                    (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) <= EndDate);
            }

            WorkItems = filtered.ToList();
        }

        /// <summary>
        /// Подсветка строк (если есть Pending заявки и т.д.)
        /// </summary>
        private async void HighlightRows()
        {
            // Получим все заявки по тем DocumentNumber, которые у нас в WorkItems
            var docNumbers = WorkItems.Select(w => w.DocumentNumber).ToList();

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
                        item.HighlightCssClass = "table-warning"; // желтоватая подсветка
                }
            }
        }
    }
}