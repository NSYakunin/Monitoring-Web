using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities;

namespace Monitoring.Infrastructure.Services.Fake
{
    public class FakeWorkItemService : IWorkItemService
    {
        public Task<List<WorkItem>> GetAllWorkItemsAsync(int divisionId)
        {
            // Вернём просто тестовые данные
            var list = new List<WorkItem>
            {
                new WorkItem
                {
                    DocumentNumber = "123/1",
                    DocumentName = "Тестовый документ",
                    WorkName = "Тестовая работа",
                    Executor = "TestExecutor",
                    Controller = "TestController",
                    Approver = "TestApprover",
                    PlanDate = DateTime.Now.AddDays(5)
                },
                new WorkItem
                {
                    DocumentNumber = "999/2",
                    DocumentName = "Второй документ",
                    WorkName = "Проверка макета",
                    Executor = "Alice",
                    Controller = "Bob",
                    Approver = "Charlie",
                    PlanDate = DateTime.Now.AddDays(10)
                }
            };

            return Task.FromResult(list);
        }

        public Task<List<string>> GetExecutorsAsync(int divisionId)
        {
            // Вернём тестовых исполнителей
            return Task.FromResult(new List<string>
            {
                "TestExecutor", "Alice", "Bob"
            });
        }

        public Task<string> GetDevAsync(int divisionId)
        {
            // Например "Отдел #Test"
            return Task.FromResult("Отдел #Test");
        }

        public void ClearCache(int divisionId)
        {
            throw new NotImplementedException();
        }

        public Task<List<WorkItem>> GetAllWorkItemsAsync(List<int> divisionIds)
        {
            throw new NotImplementedException();
        }
    }
}