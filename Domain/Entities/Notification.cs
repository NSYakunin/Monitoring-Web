// Monitoring.Domain/Entities/Notification.cs
namespace Monitoring.Domain.Entities
{
    public class Notification
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime DateSetInSystem { get; set; }
        public string UserName { get; set; } = string.Empty; // smallName пользователя, кто создал уведомление

        public bool IsActive { get; set; }
    }
}