using System.ComponentModel;

namespace EcDataguard.Contracts.Common;

public enum OsFamily
{
    Unknown = 0,
    Windows = 1,
    [Description("Linux / Unix")]
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

public enum AgentCommandType
{
    ApplyPolicy = 1,
    SetConfig = 2,
    RestartAgent = 3,
    UpdateAgent = 4,
    QuarantineDevice = 5,
    RefreshInventory = 6
}

public enum AgentCommandStatus
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

public enum PolicyEngineAction
{
    Allow = 0,
    Log = 1,
    Notify = 2,
    Block = 3,
    BlockWithOverride = 4
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

public enum TenantPlan
{
    Standard = 0,
    Premium = 1,
    Enterprise = 2
}