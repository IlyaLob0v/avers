namespace Avers.Domain;

public sealed class AppUser
{
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public string Department { get; set; } = "";
    public bool CanEditAccount { get; set; }

    /// <summary>Отключён: войти не может, его оплаты и заявки остаются.</summary>
    public bool IsDisabled { get; set; }
}
