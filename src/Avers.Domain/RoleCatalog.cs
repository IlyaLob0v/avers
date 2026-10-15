namespace Avers.Domain;

public static class RoleCatalog
{
    public static IReadOnlyList<(UserRole Role, string Title)> All { get; } =
    [
        (UserRole.Administrator, "Администратор"),
        (UserRole.ChiefEngineer, "Главный инженер"),
        (UserRole.AntennaServiceHead, "Начальник антенной службы"),
        (UserRole.AdsHead, "Начальник АДС"),
        (UserRole.Dispatcher, "Пользователь АДС (диспетчер)"),
        (UserRole.Lawyer, "Юрист"),
        (UserRole.Reader, "Читатель (радиомеханик)")
    ];

    public static string Title(UserRole role) => All.First(x => x.Role == role).Title;

    public static bool Can(UserRole role, AppModule module, out AccessLevel level)
    {
        level = Matrix.TryGetValue((role, module), out var found) ? found : AccessLevel.None;
        return level != AccessLevel.None;
    }

    /// <summary>
    /// Править любую заявку может начальник АДС и администратор. Пользователь АДС (диспетчер)
    /// правит только заявки, которые принял сам; чужие он видит, но не меняет.
    /// </summary>
    public static bool CanEditTicket(UserRole role, int userId, WorkTicket ticket) => role switch
    {
        UserRole.Administrator or UserRole.AdsHead => true,
        UserRole.Dispatcher => ticket.AcceptedByUserId == userId,
        _ => false
    };

    /// <summary>Видит все заявки и правит любые: начальник АДС или администратор.</summary>
    public static bool IsTicketSupervisor(UserRole role) => role is UserRole.Administrator or UserRole.AdsHead;

    public enum AccessLevel { None, View, Edit, ScreenOnly }

    private static readonly Dictionary<(UserRole, AppModule), AccessLevel> Matrix = new()
    {
        { (UserRole.Administrator, AppModule.Abon), AccessLevel.Edit },
        { (UserRole.Administrator, AppModule.Disp), AccessLevel.Edit },
        { (UserRole.Administrator, AppModule.Anten), AccessLevel.Edit },
        { (UserRole.Administrator, AppModule.Scheme), AccessLevel.Edit },
        { (UserRole.Administrator, AppModule.Legal), AccessLevel.Edit },
        { (UserRole.Administrator, AppModule.Phonebook), AccessLevel.Edit },
        { (UserRole.ChiefEngineer, AppModule.Abon), AccessLevel.View },
        { (UserRole.ChiefEngineer, AppModule.Disp), AccessLevel.View },
        { (UserRole.ChiefEngineer, AppModule.Anten), AccessLevel.Edit },
        { (UserRole.ChiefEngineer, AppModule.Scheme), AccessLevel.Edit },
        { (UserRole.ChiefEngineer, AppModule.Legal), AccessLevel.View },
        { (UserRole.ChiefEngineer, AppModule.Phonebook), AccessLevel.Edit },
        { (UserRole.AntennaServiceHead, AppModule.Abon), AccessLevel.View },
        { (UserRole.AntennaServiceHead, AppModule.Disp), AccessLevel.View },
        { (UserRole.AntennaServiceHead, AppModule.Anten), AccessLevel.View },
        { (UserRole.AntennaServiceHead, AppModule.Scheme), AccessLevel.View },
        { (UserRole.AntennaServiceHead, AppModule.Legal), AccessLevel.View },
        { (UserRole.AntennaServiceHead, AppModule.Phonebook), AccessLevel.View },
        { (UserRole.AdsHead, AppModule.Abon), AccessLevel.Edit },
        { (UserRole.AdsHead, AppModule.Disp), AccessLevel.Edit },
        { (UserRole.AdsHead, AppModule.Anten), AccessLevel.View },
        { (UserRole.AdsHead, AppModule.Scheme), AccessLevel.View },
        { (UserRole.AdsHead, AppModule.Legal), AccessLevel.View },
        { (UserRole.AdsHead, AppModule.Phonebook), AccessLevel.View },
        { (UserRole.Dispatcher, AppModule.Abon), AccessLevel.View },
        { (UserRole.Dispatcher, AppModule.Disp), AccessLevel.Edit },
        { (UserRole.Dispatcher, AppModule.Anten), AccessLevel.View },
        { (UserRole.Dispatcher, AppModule.Scheme), AccessLevel.View },
        { (UserRole.Lawyer, AppModule.Abon), AccessLevel.View },
        { (UserRole.Lawyer, AppModule.Legal), AccessLevel.Edit },
        { (UserRole.Reader, AppModule.Anten), AccessLevel.View },
        { (UserRole.Reader, AppModule.Scheme), AccessLevel.ScreenOnly },
        { (UserRole.Reader, AppModule.Reader), AccessLevel.View },
    };
}
