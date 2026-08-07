namespace EcDataguard.Domain.Enums;

public enum OsType
{
    Unknown = 0,
    Windows = 1,
    Linux = 2,
    MacOs = 3
}

public enum ProtectionState
{
    Unknown = 0,
    Protected = 1,
    Degraded = 2,
    Unprotected = 3
}

public enum PolicyAction
{
    Allow = 0,
    Log = 1,
    Notify = 2,
    Block = 3,
    BlockWithOverride = 4
}

public enum PolicyKind
{
    Data = 0,
    Application = 1,
    Website = 2,
    ExternalDevice = 3,
    Auditing = 4
}

public enum AgentCommandKind
{
    ApplyPolicy = 1,
    SetConfig = 2,
    RestartAgent = 3,
    UpdateAgent = 4,
    QuarantineDevice = 5,
    RefreshInventory = 6
}

public enum CommandState
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Skipped = 3,
    Expired = 4
}

public enum EventKind
{
    FileOp = 1,
    Usb = 2,
    Web = 3,
    App = 4,
    DbFound = 5,
    ConfigError = 6,
    AgentEvent = 7
}

public enum DbEngine
{
    Unknown = 0,
    MsSql = 1,
    PostgreSql = 2,
    MySql = 3,
    MariaDb = 4,
    Oracle = 5,
    MongoDb = 6,
    Redis = 7,
    Elasticsearch = 8
}

public enum InsightSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum InsightStatus
{
    Open = 0,
    Closed = 1
}

public enum Role
{
    SuperAdmin = 0,
    TenantAdmin = 1,
    Auditor = 2
}