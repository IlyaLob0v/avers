namespace Avers.Contracts;

/// <summary>Домофонная панель подъезда. Запрос заменяет все поля целиком.</summary>
public sealed record IntercomPanelRequest(int Entrance, string? PanelType, string? SwitchBlock, string? Keys, string? Power);

/// <summary>Новая заявка по домофону.</summary>
public sealed record IntercomTicketRequest(int Entrance, string? Fault, string? Mechanic);
