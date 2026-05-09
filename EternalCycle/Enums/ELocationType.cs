namespace VulcanCore;

[Flags]
public enum ELocationType
{
    None = 0,
    Custom = 1 << 0,            // 1
    Woods = 1 << 1,             // 2
    Factory_Day = 1 << 2,       // 4
    Factory_Night = 1 << 3,     // 8
    Laboratory = 1 << 4,        // 16
    Shoreline = 1 << 5,         // 32
    ReserveBase = 1 << 6,       // 64
    Interchange = 1 << 7,       // 128
    Lighthouse = 1 << 8,        // 256
    TarkovStreets = 1 << 9,     // 512
    GroundZero = 1 << 10,       // 1024
    GroundZero_High = 1 << 11,  // 2048
    Labyrinth = 1 << 12         // 4096
}