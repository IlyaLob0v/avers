namespace Avers.Domain;

/// <summary>
/// Организация, обслуживающая дом. В старом Аверсе поле называлось «домовладелец»; сотрудники просят
/// другое название (REQ-ENG-02: не «УК» и не «домовладелец»), в новом — «обслуживающая организация».
/// </summary>
public static class ServiceOrganizations
{
    public static string Title(Organization? org) => org switch
    {
        null => "не указана",
        { Branch: var b } when !string.IsNullOrWhiteSpace(b) => $"{org.Name}, {b}",
        _ => org.Name
    };
}
