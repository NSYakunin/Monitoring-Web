using Microsoft.AspNetCore.Mvc.Rendering;

namespace web_test.Services
{
    public interface IWorkItemService
    {
        /// <summary>
        /// Получить список исполнителей для указанного подразделения.
        /// </summary>
        Task<List<SelectListItem>> GetExecutorsAsync(int divisionId);

        /// <summary>
        /// Получить (и при необходимости фильтровать) список WorkItem для указанного подразделения.
        /// </summary>
        Task<List<WorkItem>> GetWorkItemsAsync(
            int divisionId,
            DateTime startDate,
            DateTime endDate,
            string executor,
            string search
        );
    }
}