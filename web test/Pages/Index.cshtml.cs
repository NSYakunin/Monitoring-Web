using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Application.Services;
using Monitoring.Domain.Entities;
using Monitoring.Infrastructure.Services;
using System.Text.Json; // Для JsonSerializer

namespace Monitoring.UI.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IWorkRequestService _workRequestService;
        private readonly IWorkItemService _workItemService;
        private readonly INotificationService _notificationService;

        public IndexModel(IWorkItemService workItemService, INotificationService notificationService, IWorkRequestService workRequestService)
        {
            _workItemService = workItemService;
            _notificationService = notificationService;
            _workRequestService = workRequestService;
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

        public async Task OnGet()
        {
            // Пример чтения куки
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
            {
                Response.Redirect("/Login");
                return;
            }

            int divisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);
            UserName = HttpContext.Request.Cookies["userName"];


            // Установка дат по умолчанию, если не заданы
            if (!StartDate.HasValue)
                StartDate = new DateTime(2014, 1, 1);

            DateTime now = DateTime.Now;
            if (!EndDate.HasValue)
                EndDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);

            // Деактивируем старые уведомления:
            await _notificationService.DeactivateOldNotificationsAsync(90);

            // Получаем активные уведомления:
            Notifications = await _notificationService.GetActiveNotificationsAsync(divisionId);

            // Загружаем данные
            Executors = await _workItemService.GetExecutorsAsync(divisionId);
            WorkItems = await _workItemService.GetAllWorkItemsAsync(divisionId);
            DepartmentName = await _workItemService.GetDevAsync(divisionId);


            // Применяем фильтры к WorkItems
            ApplyFilters();

            // Подсветка строк (смотрим заявки)
            HighlightRows();
        }

        public async Task<IActionResult> OnGetFilterAsync(
                        DateTime? startDate,
                        DateTime? endDate,
                        string? executor,
                        string? searchQuery)
        {
            // AJAX-обработчик, вызываемый при изменении фильтров
            // 1) Проверяем куки
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
                return new JsonResult(new { error = "Не найдены куки divisionId" });

            // 2) Считываем всё то же, что и в OnGet
            int divisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);
            UserName = HttpContext.Request.Cookies["userName"]; // <-- ВАЖНО!!! Присваиваем, чтобы partial знал пользователя

            // 3) Устанавливаем поля модели
            StartDate = startDate ?? new DateTime(2014, 1, 1);
            EndDate = endDate ?? DateTime.Now;
            Executor = executor;
            SearchQuery = searchQuery;

            // 4) Подгружаем данные, как и в OnGet
            Executors = await _workItemService.GetExecutorsAsync(divisionId);
            WorkItems = await _workItemService.GetAllWorkItemsAsync(divisionId);
            DepartmentName = await _workItemService.GetDevAsync(divisionId);

            // 5) Фильтруем + выделяем строки с заявками
            ApplyFilters();

            // Подсветка строк (смотрим заявки)
            HighlightRows();

            // Возвращаем partial (HTML-фрагмент) с таблицей
            return Partial("_WorkItemsTablePartial", this);
        }

        // Метод, который помечает WorkItem'ы, у которых есть Pending-заявки
        private async void HighlightRows()
        {
            // Получим все заявки по тем DocumentNumber, которые у нас в WorkItems
            // Можно сделать единый запрос, если нужно
            var docNumbers = WorkItems.Select(w => w.DocumentNumber).ToList();

            foreach (var item in WorkItems)
            {
                // Загрузим заявки для каждого WorkItem
                var requests = await _workRequestService.GetRequestsByDocumentNumberAsync(item.DocumentNumber);
                // Ищем актуальные (Pending)
                var pendingRequests = requests.Where(r => r.Status == "Pending" && !r.IsDone).ToList();

                // Если есть заявка "факт" => будем красить голубым
                // Если есть заявка "корр1/корр2/корр3" => красить коричневым
                if (pendingRequests.Any())
                {
                    bool hasFact = pendingRequests.Any(r => r.RequestType == "fact");
                    bool hasCorr = pendingRequests.Any(r => r.RequestType.StartsWith("корр"));

                    // Сохраним для Frontend - можно завести поле item.HighlightCssClass
                    // или как-то иначе (доп. свойство в WorkItem).
                    if (hasFact)
                        item.HighlightCssClass = "table-info"; // голубой (Bootstrap)
                    else if (hasCorr)
                        item.HighlightCssClass = "table-warning"; // коричневато-жёлтый (Bootstrap)
                }
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Генерация PDF
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
                return RedirectToPage("/Login");

            int divisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);

            Executors = await _workItemService.GetExecutorsAsync(divisionId);
            WorkItems = await _workItemService.GetAllWorkItemsAsync(divisionId);
            string dev = await _workItemService.GetDevAsync(divisionId);

            ApplyFilters();

            // Обработка выбранных позиций (SelectedItemsOrder)
            if (!string.IsNullOrEmpty(SelectedItemsOrder))
            {
                var selectedList = JsonSerializer.Deserialize<List<string>>(SelectedItemsOrder);
                if (selectedList != null && selectedList.Count > 0)
                {
                    // Фильтруем WorkItems, оставляем только те, DocumentNumber которых есть в selectedList
                    var filtered = WorkItems.Where(w => selectedList.Contains(w.DocumentNumber)).ToList();

                    // Сортируем в порядке, в котором DocumentNumber идут в selectedList
                    filtered = filtered.OrderBy(w => selectedList.IndexOf(w.DocumentNumber)).ToList();

                    WorkItems = filtered;
                }
            }

            // Генерация pdfBytes (ReportGenerator - ваш сервис/утилита, не показан в коде)
            var pdfBytes = ReportGenerator.GeneratePdf(WorkItems,
                          $"Сдаточный чек от {DateTime.Now.ToShortDateString()}", dev);

            return File(pdfBytes, "application/pdf", $"Чек_{DateTime.Now:yyyyMMdd}.pdf");
        }

        private void ApplyFilters()
        {
            // Применение выбранных фильтров
            var filtered = WorkItems.AsQueryable();

            if (!string.IsNullOrEmpty(Executor))
            {
                filtered = filtered.Where(x => x.Executor.Contains(Executor, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(SearchQuery))
            {
                filtered = filtered.Where(x =>
                    (x.DocumentName != null && x.DocumentName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (x.WorkName != null && x.WorkName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Executor != null && x.Executor.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Controller != null && x.Controller.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Approver != null && x.Approver.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)));
            }

            if (EndDate.HasValue)
            {
                filtered = filtered.Where(x =>
                    (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) <= EndDate);
            }

            WorkItems = filtered.ToList();
        }

        // Хендлер для создания заявки
        [IgnoreAntiforgeryToken] // упрощаем
        public async Task<IActionResult> OnPostCreateRequestAsync()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string body = await reader.ReadToEndAsync();

                var dto = JsonSerializer.Deserialize<CreateRequestDto>(body);
                if (dto == null)
                    return new JsonResult(new { success = false, message = "Невалидный JSON" });

                // Проверка: текущий пользователь == dto.Sender и он должен входить в "Executor" списка
                // Для упрощения: сделаем маленькую проверку
                // Загружаем WorkItem
                // Загружаем данные
                int divisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);
                UserName = HttpContext.Request.Cookies["userName"];

                Executors = await _workItemService.GetExecutorsAsync(divisionId);
                WorkItems = await _workItemService.GetAllWorkItemsAsync(divisionId);
                DepartmentName = await _workItemService.GetDevAsync(divisionId);

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

        // Хендлер для принятия/отклонения заявки
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
                // Для этого нужно загрузить заявку по Id
                // (Заметим, что в OnGet мы не грузили все заявки в отдельное свойство,
                //   поэтому сделаем отдельный запрос)
                // Но если WorkItems уже загружены, можно кэшировать. Упростим.
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

        public async Task<IActionResult> OnGetMyRequestsAsync()
        {
            // 1) Проверяем, что пользователь зашёл
            if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
            {
                // Можно вернуть пустой список или ошибку
                return new JsonResult(new { success = false, message = "Не найдены куки divisionId" });
            }

            // 2) Считываем userName
            string userName = HttpContext.Request.Cookies["userName"];
            UserName = userName; // чтобы при необходимости дальше использовать

            // 3) Загружаем все заявки, но фильтруем только те, где Receiver == userName и Status == "Pending" и не IsDone
            //    - либо вызываем специальный метод _workRequestService.GetRequestsForReceiverAsync(userName)
            //      если у вас такого нет — легко добавить, или просто получить все Requests, а потом отфильтровать.
            var allRequests = await _workRequestService.GetAllRequestsAsync(); // пример
            var myPendingRequests = allRequests
                .Where(r => r.Receiver == userName && r.Status == "Pending" && !r.IsDone)
                .ToList();

            // 4) Для удобства на клиенте вернём анонимный объект
            var result = myPendingRequests.Select(r => new {
                id = r.Id,
                workDocumentNumber = r.WorkDocumentNumber,
                requestType = r.RequestType,
                proposedDate = r.ProposedDate?.ToString("yyyy-MM-dd"),
                sender = r.Sender,
                note = r.Note
            });

            // 5) Возвращаем JSON
            return new JsonResult(result);
        }

        [IgnoreAntiforgeryToken] // либо добавить валидацию, если нужно
        public async Task<IActionResult> OnPostRefreshCacheAsync()
        {
            try
            {
                // Предположим, в IWorkItemService есть метод ClearCache(int divisionId) или что-то похожее:
                if (!HttpContext.Request.Cookies.ContainsKey("divisionId"))
                    return new JsonResult(new { success = false, message = "No division cookie." });

                int divisionId = int.Parse(HttpContext.Request.Cookies["divisionId"]);

                // Сбрасываем кэш
                _workItemService.ClearCache(divisionId);

                // Дополнительно, можно заново подгрузить WorkItems и вернуть их в JSON, 
                // но если вы всё равно перезагружаете всю страницу, это не обязательно.
                // Например:
                // await _workItemService.GetAllWorkItemsAsync(divisionId);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        public IActionResult OnGetLogout()
        {
            // Логаут
            HttpContext.Response.Cookies.Delete("userName");
            HttpContext.Response.Cookies.Delete("divisionId");
            return RedirectToPage("Login");
        }
    }
}