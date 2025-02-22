// Monitoring.Application/Interfaces/IWorkItemService.cs
using Monitoring.Domain.Entities;

namespace Monitoring.Application.Interfaces
{
    /// <summary>
    /// Интерфейс для работы с WorkItem'ами
    /// (загрузка, фильтрация, пр. операции).
    /// </summary>
    public interface IWorkItemService
    {
        Task<List<WorkItem>> GetAllWorkItemsAsync(List<int> divisionIds);

        Task<List<string>> GetExecutorsAsync(int divisionId);

        Task<string> GetDevAsync(int divisionId);

        // Можешь добавить здесь сигнатуры методов для фильтрации,
        // создания, обновления и т.п.

        void ClearCache(int divisionId);
    }
}