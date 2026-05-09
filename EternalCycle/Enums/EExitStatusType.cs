namespace VulcanCore;

[Flags]
public enum EExitStatusType
{
    None = 0,
    Survived = 1 << 0,          // 1
    Runner = 1 << 1,            // 2
    Killed = 1 << 2,            // 4
    MissingInAction = 1 << 3,   // 8
    Left = 1 << 4,              // 16
    Transit = 1 << 5            // 32
}