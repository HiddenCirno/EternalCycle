namespace VulcanCore;

[Flags]
public enum EQuestStatusType
{
    None = 0,                       // Ä¬ÈÏÃ»ÓÐ×´Ì¬
    Locked = 1 << 0,                // 1
    AvailableForStart = 1 << 1,     // 2
    Started = 1 << 2,               // 4
    AvailableForFinish = 1 << 3,    // 8
    Success = 1 << 4,               // 16
    Fail = 1 << 5,                  // 32
    FailRestartable = 1 << 6,       // 64
    MarkedAsFailed = 1 << 7,        // 128
    Expired = 1 << 8,               // 256
    AvailableAfter = 1 << 9         // 512
}