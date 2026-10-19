namespace Avers.Contracts;

/// <summary>Отметка месяца в графике ППР: Empty, Planned или Done.</summary>
public sealed record PprMarkRequest(string Status);

/// <summary>Сколько работ перенесено в план следующего года.</summary>
public sealed record PprCopyResult(int Year, int Copied);
