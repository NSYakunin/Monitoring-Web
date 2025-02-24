using Monitoring.Application.DTO;

namespace Monitoring.Application.Interfaces
{
    public interface IUserSettingsService
    {
        // Проверить, есть ли у пользователя право на доступ к настройкам
        Task<bool> HasAccessToSettingsAsync(int userId);

        // Получить объект PrivacySettings для пользователя
        Task<PrivacySettingsDto> GetPrivacySettingsAsync(int userId);

        // Сохранить объект PrivacySettings для пользователя
        Task SavePrivacySettingsAsync(int userId, PrivacySettingsDto dto);

        // Получить список всех доступных подразделений (справочник)
        Task<List<DivisionDto>> GetAllDivisionsAsync();

        // Получить список id подразделений, к которым есть доступ у конкретного пользователя
        Task<List<int>> GetUserAllowedDivisionsAsync(int userId);

        // Сохранить список id подразделений, доступных пользователю
        Task SaveUserAllowedDivisionsAsync(int userId, List<int> divisionIds);

        Task<int> RegisterUserInDbAsync(
            string fullName,
            string smallName,
            string password,
            int? idDivision,
            bool canCloseWork,
            bool canSendCloseRequest,
            bool canAccessSettings
        );

        Task ChangeUserPasswordAsync(int userId, string newPassword);
    }
}