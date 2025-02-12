using Monitoring.Application.Interfaces;

namespace Monitoring.Infrastructure.Services.Fake
{
    public class FakeLoginService : ILoginService
    {
        public Task<List<string>> GetAllUsersAsync()
        {
            return Task.FromResult(new List<string> { "TestUser1", "TestUser2" });
        }

        public Task<List<string>> FilterUsersAsync(string query)
        {
            // Для примера, просто возвращаем 1-2 пользователей
            var all = new List<string> { "TestUser1", "TestUser2" };
            return Task.FromResult(all.Where(u => u.Contains(query)).ToList());
        }

        public Task<(int? divisionId, bool isValid)> CheckUserCredentialsAsync(string selectedUser, string password)
        {
            // Любого пускаем с divisionId = 999
            if (string.IsNullOrEmpty(selectedUser) || string.IsNullOrEmpty(password))
                return Task.FromResult<(int?, bool)>((null, false));

            return Task.FromResult<(int?, bool)>((999, true));
        }
    }
}