using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities;

namespace Monitoring.UI.Pages
{
    public class MyRequestsModel : PageModel
    {
        private readonly IWorkRequestService _workRequestService;
        private readonly IUserSettingsService _userSettingsService;
        private readonly ILoginService _loginService;

        public MyRequestsModel(
            IWorkRequestService workRequestService,
            IUserSettingsService userSettingsService,
            ILoginService loginService)
        {
            _workRequestService = workRequestService;
            _userSettingsService = userSettingsService;
            _loginService = loginService;
        }

        public bool HasCloseWorkAccess { get; set; }
        public List<WorkRequest> MyRequests { get; set; } = new();

        public async Task OnGet()
        {
            // ��������� ����
            if (!HttpContext.Request.Cookies.ContainsKey("userName"))
            {
                Response.Redirect("/Login");
                return;
            }

            string userName = HttpContext.Request.Cookies["userName"];
            int? userId = await _loginService.GetUserIdByNameAsync(userName);
            if (userId == null)
            {
                Response.Redirect("/Login");
                return;
            }

            // ��������� ������ �� �������� �����
            HasCloseWorkAccess = await _userSettingsService.HasAccessToCloseWorkAsync(userId.Value);

            if (HasCloseWorkAccess)
            {
                // ���� ������ ���� � ���������� Pending ������, ��� Receiver == userName
                var pending = await _workRequestService.GetPendingRequestsByReceiverAsync(userName);
                // ����� ����������� ������ Pending, �� ����� ��� ���������� Pending
                // ���� �����, ���������, ��� GetPendingRequestsByReceiverAsync ���������� ������ Pending
                // ���� ���, �� ���� �����������:
                MyRequests = pending.Where(r => r.Status == "Pending").ToList();
            }
        }

        // POST: SetRequestStatus
        public async Task<IActionResult> OnPostSetRequestStatusAsync()
        {
            if (!HttpContext.Request.Cookies.ContainsKey("userName"))
            {
                return new JsonResult(new { success = false, message = "No cookies" });
            }

            string userName = HttpContext.Request.Cookies["userName"];
            using var reader = new StreamReader(Request.Body);
            string body = await reader.ReadToEndAsync();

            try
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<StatusChangeDto>(body);
                if (data == null)
                    return new JsonResult(new { success = false, message = "���������� ������" });

                // ���������, ��� ������� ������������ == Receiver
                // (������� ���� �������� � �������, ��� ����� ���)
                var allForDoc = await _workRequestService.GetRequestsByDocumentNumberAsync(data.DocumentNumber);
                var req = allForDoc.FirstOrDefault(r => r.Id == data.RequestId);
                if (req == null)
                    return new JsonResult(new { success = false, message = "������ �� �������" });

                if (req.Receiver != userName)
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "� ��� ��� ���� �� ��������� ������� ���� ������."
                    });
                }

                if (data.NewStatus != "Accepted" && data.NewStatus != "Declined")
                    return new JsonResult(new { success = false, message = "������������ ������" });

                await _workRequestService.SetRequestStatusAsync(data.RequestId, data.NewStatus);

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }

    // DTO
    public class StatusChangeDto
    {
        public int RequestId { get; set; }
        public string DocumentNumber { get; set; }
        public string NewStatus { get; set; }
    }
}