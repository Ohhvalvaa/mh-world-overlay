namespace MHWorldOverlay;

internal enum Element { Fire, Water, Thunder, Ice, Dragon }

/// <summary>
/// Dati fissi dei mostri: soglia di cattura (frazione della vita massima, dove compare il teschio;
/// 0 = non catturabile) e debolezze elementali. Generati da HunterPie/Game/World/Data/MonsterData.xml;
/// gli Id coincidono con SharpPluginLoader.Core.Entities.MonsterType.
/// </summary>
internal static class MonsterData
{
    public readonly record struct Info(float Capture, Element[] Weaknesses);

    private static readonly Dictionary<uint, Info> ById = new()
    {
        [0] = new(25 / 100f, [Element.Water, Element.Ice]),
        [1] = new(20 / 100f, [Element.Dragon, Element.Thunder]),
        [4] = new(0 / 100f, [Element.Dragon, Element.Water]),
        [7] = new(30 / 100f, [Element.Fire, Element.Thunder]),
        [9] = new(25 / 100f, [Element.Dragon, Element.Thunder]),
        [10] = new(25 / 100f, [Element.Dragon, Element.Thunder]),
        [11] = new(15 / 100f, [Element.Dragon, Element.Ice]),
        [12] = new(20 / 100f, [Element.Ice, Element.Water]),
        [13] = new(15 / 100f, [Element.Ice, Element.Water]),
        [14] = new(0 / 100f, [Element.Fire, Element.Water]),
        [15] = new(0 / 100f, [Element.Dragon, Element.Water]),
        [16] = new(0 / 100f, [Element.Thunder, Element.Dragon]),
        [17] = new(0 / 100f, [Element.Ice, Element.Dragon]),
        [18] = new(0 / 100f, [Element.Water, Element.Ice]),
        [19] = new(20 / 100f, [Element.Water, Element.Ice]),
        [20] = new(20 / 100f, [Element.Dragon, Element.Thunder]),
        [21] = new(25 / 100f, [Element.Water, Element.Fire]),
        [22] = new(20 / 100f, [Element.Water, Element.Dragon]),
        [23] = new(0 / 100f, [Element.Fire, Element.Thunder]),
        [24] = new(25 / 100f, [Element.Thunder, Element.Ice]),
        [25] = new(0 / 100f, [Element.Thunder, Element.Dragon]),
        [26] = new(0 / 100f, [Element.Fire, Element.Dragon]),
        [27] = new(30 / 100f, [Element.Water, Element.Fire]),
        [28] = new(30 / 100f, [Element.Ice, Element.Thunder]),
        [29] = new(25 / 100f, [Element.Thunder, Element.Water]),
        [30] = new(25 / 100f, [Element.Water, Element.Fire]),
        [31] = new(25 / 100f, [Element.Fire, Element.Thunder]),
        [32] = new(20 / 100f, [Element.Thunder, Element.Fire]),
        [33] = new(30 / 100f, [Element.Water, Element.Ice]),
        [34] = new(20 / 100f, [Element.Ice, Element.Thunder]),
        [35] = new(25 / 100f, [Element.Dragon, Element.Ice]),
        [36] = new(0 / 100f, [Element.Fire, Element.Dragon]),
        [37] = new(30 / 100f, [Element.Thunder, Element.Ice]),
        [38] = new(0 / 100f, [Element.Ice, Element.Thunder]),
        [39] = new(30 / 100f, [Element.Thunder, Element.Dragon]),
        [51] = new(0 / 100f, [Element.Fire, Element.Thunder]),
        [61] = new(20 / 100f, [Element.Thunder, Element.Dragon]),
        [62] = new(20 / 100f, [Element.Thunder, Element.Fire]),
        [63] = new(20 / 100f, [Element.Fire, Element.Thunder]),
        [64] = new(10 / 100f, [Element.Dragon, Element.Thunder]),
        [65] = new(20 / 100f, [Element.Ice, Element.Water]),
        [66] = new(20 / 100f, [Element.Water, Element.Ice]),
        [67] = new(20 / 100f, [Element.Fire, Element.Thunder]),
        [68] = new(15 / 100f, [Element.Ice, Element.Water]),
        [69] = new(25 / 100f, [Element.Ice, Element.Thunder]),
        [70] = new(0 / 100f, [Element.Dragon, Element.Thunder]),
        [71] = new(25 / 100f, [Element.Thunder, Element.Ice]),
        [72] = new(25 / 100f, [Element.Water, Element.Fire]),
        [73] = new(15 / 100f, [Element.Fire, Element.Thunder]),
        [74] = new(15 / 100f, [Element.Water, Element.Thunder]),
        [75] = new(0 / 100f, [Element.Fire, Element.Dragon]),
        [76] = new(10 / 100f, [Element.Ice, Element.Thunder]),
        [77] = new(25 / 100f, [Element.Fire, Element.Thunder]),
        [78] = new(25 / 100f, [Element.Dragon, Element.Fire]),
        [79] = new(0 / 100f, [Element.Fire, Element.Dragon]),
        [80] = new(0 / 100f, [Element.Fire, Element.Dragon]),
        [81] = new(0 / 100f, [Element.Ice, Element.Water]),
        [87] = new(0 / 100f, [Element.Ice, Element.Fire, Element.Dragon]),
        [88] = new(10 / 100f, [Element.Thunder, Element.Water]),
        [89] = new(10 / 100f, [Element.Water, Element.Thunder]),
        [90] = new(20 / 100f, [Element.Water, Element.Dragon]),
        [91] = new(15 / 100f, [Element.Ice, Element.Water]),
        [92] = new(10 / 100f, [Element.Ice, Element.Water]),
        [93] = new(15 / 100f, [Element.Water, Element.Thunder]),
        [94] = new(20 / 100f, [Element.Ice, Element.Water]),
        [95] = new(20 / 100f, [Element.Thunder, Element.Fire]),
        [96] = new(0 / 100f, [Element.Ice, Element.Water]),
        [97] = new(0 / 100f, [Element.Dragon, Element.Thunder]),
        [98] = new(0 / 100f, []),
        [99] = new(15 / 100f, [Element.Water, Element.Dragon]),
        [100] = new(20 / 100f, [Element.Fire, Element.Thunder]),
        [101] = new(0 / 100f, [Element.Dragon, Element.Fire]),
    };

    public static Info? For(uint monsterId) => ById.TryGetValue(monsterId, out var info) ? info : null;
}
