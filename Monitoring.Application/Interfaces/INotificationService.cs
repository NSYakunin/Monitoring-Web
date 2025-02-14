// Monitoring.Application/Interfaces/INotificationService.cs
using Monitoring.Domain.Entities;

namespace Monitoring.Application.Interfaces
{
    public interface INotificationService
    {
        /// <summary>
        /// Деактивирует уведомления, которые старше заданного периода (например, 90 дней).
        /// </summary>
        Task DeactivateOldNotificationsAsync(int days);

        /// <summary>
        /// Получает список активных уведомлений для указанного divisionId.
        /// </summary>
        Task<List<Notification>> GetActiveNotificationsAsync(int divisionId);
    }
}