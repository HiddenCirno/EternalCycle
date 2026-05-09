namespace VulcanCore;

[Flags]
public enum EGameVersionType
{
    none = 0,
    standard = 1 << 0,            // 1
    left_behind = 1 << 1,         // 2
    prepare_for_escape = 1 << 2,  // 4
    edge_of_darkness = 1 << 3,    // 8
    unheard_edition = 1 << 4,     // 16
    develop = 1 << 5,             // 32
    tournament = 1 << 6,          // 64
    tournament_live = 1 << 7,     // 128
    press_edition = 1 << 8,       // 256
    exhibition = 1 << 9           // 512
}