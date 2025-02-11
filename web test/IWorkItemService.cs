using Microsoft.AspNetCore.Mvc.Rendering;

namespace Monitoring.UI
{
    public interface IWorkItemService
    {
        Task<List<WorkItem>> GetAllWorkItemsAsync(int divisionId);
        Task<List<SelectListItem>> GetExecutorsAsync(int divisionId);

        Task<string> GetDevAsync(int divisionId);
    }
}
