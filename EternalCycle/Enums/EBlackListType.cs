namespace VulcanCore;

[Flags]
public enum EBlackListType
{
    None = 0,
    AirDrop = 1 << 0,       // 1
    PMCLoot = 1 << 1,       // 2
    ScavCaseLoot = 1 << 2,  // 4
    Fence = 1 << 3,         // 8
    Circle = 1 << 4,        // 16
    DailyReward = 1 << 5,   // 32
    Global = 1 << 6         // 64
}