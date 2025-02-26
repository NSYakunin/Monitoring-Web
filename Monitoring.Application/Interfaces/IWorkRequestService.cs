using Monitoring.Domain.Entities;

namespace Monitoring.Application.Interfaces
{
    public interface IWorkRequestService
    {
        Task CreateRequestAsync(WorkRequest request);
        Task<List<WorkRequest>> GetRequestsByDocumentNumberAsync(string docNumber);

        /// <summary>Изменить статус заявки (Accepted/Declined) + проставить IsDone=1.</summary>
        Task SetRequestStatusAsync(int requestId, string newStatus);

        Task<List<WorkRequest>> GetPendingRequestsByReceiverAsync(string receiver);
    }
}