// Monitoring.Application/Interfaces/IWorkItemService.cs
using Monitoring.Domain.Entities;

namespace Monitoring.Application.Interfaces
{
    /// <summary>
    /// Интерфейс для работы с WorkItem (Со списком работ)
    /// (загрузка, фильтрация, пр. операции).
    /// </summary>
    public interface IWorkItemService
    {
        Task<List<WorkItem>> GetAllWorkItemsAsync(List<int> divisionIds);

        Task<List<string>> GetExecutorsAsync(int divisionId);

        Task<string> GetDevAsync(int divisionId);

        void ClearCache(int divisionId);

        Task<List<string>> GetApproversAsync(int divisionId);
    }
}