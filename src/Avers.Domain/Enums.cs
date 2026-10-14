namespace Avers.Domain;

public enum UserRole
{
    Administrator,
    ChiefEngineer,
    AntennaServiceHead,
    AdsHead,
    Dispatcher,
    Lawyer,
    Reader
}

public enum TicketStatus
{
    Accepted,
    InProgress,
    Done,
    Deferred
}

public enum TicketKind
{
    Antenna,
    Cable,
    Amplifier,
    Intercom,
    Provider,
    Other
}

public enum PprStatus
{
    Empty,
    Planned,
    Done,
    Overdue
}

public enum SearchMode
{
    Address,
    Pzhreu,
    Account,
    InnerCode,
    Surname
}

public enum AppModule
{
    Desktop,
    Abon,
    Disp,
    Ticket,
    Anten,
    AntenInfo,
    Scheme,
    Subscriber,
    PrDubl,
    Contract,
    Print,
    Ppr,
    Legal,
    Claim,
    Reports,
    Phonebook,
    Domofo,
    Reader,
    Access
}
