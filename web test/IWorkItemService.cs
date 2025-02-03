using Microsoft.AspNetCore.Mvc.Rendering;

namespace web_test
{
    public interface IWorkItemService
    {
        Task<List<WorkItem>> LoadWorkItemsAsync(int divisionId, DateTime startDate, DateTime endDate, string executor);
        Task<List<SelectListItem>> LoadExecutorsAsync(int divisionId);
    }
}
