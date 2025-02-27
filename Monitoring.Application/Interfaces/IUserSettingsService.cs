using Monitoring.Application.DTO;

namespace Monitoring.Application.Interfaces
{
    public interface IUserSettingsService
    {
        // Проверить, есть ли у пользователя право на доступ к настройкам
        Task<bool> HasAccessToSettingsAsync(int userId);

        // Проверить, есть ли у пользователя право на доступ к отправке заявок за закрытие или перенос работ
        Task<bool> HasAccessToSendCloseRequestAsync(int userId);

        // Проверить, есть ли у пользователя право на доступ к возможности закрывать заявоки на закрытие или перенос работ
        Task<bool> HasAccessToCloseWorkAsync(int userId);

        // Получить объект PrivacySettings для пользователя
        Task<PrivacySettingsDto> GetPrivacySettingsAsync(int userId);

        // Новый вариант, где передаём ещё и статус "Isvalid" (активен/неактивен)
        Task SavePrivacySettingsAsync(int userId, PrivacySettingsDto dto, bool isActive);

        // Получить список всех доступных подразделений (справочник)
        Task<List<DivisionDto>> GetAllDivisionsAsync();

        // Получить список id подразделений, к которым есть доступ у конкретного пользователя
        Task<List<int>> GetUserAllowedDivisionsAsync(int userId);

        // Сохранить список id подразделений, доступных пользователю
        Task SaveUserAllowedDivisionsAsync(int userId, List<int> divisionIds);

        // Регистрация нового пользователя
        Task<int> RegisterUserInDbAsync(
            string fullName,
            string smallName,
            string password,
            int? idDivision,
            bool canCloseWork,
            bool canSendCloseRequest,
            bool canAccessSettings
        );

        // Поменять пароль у пользователя
        Task ChangeUserPasswordAsync(int userId, string newPassword);

  
        // Получить текущий пароль пользователя (из таблицы Users.Password)
        Task<string?> GetUserCurrentPasswordAsync(int userId);
    }
}