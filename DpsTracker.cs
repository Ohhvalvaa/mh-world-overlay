using System.Diagnostics;

namespace MHWorldOverlay;

/// <summary>
/// DPS di ogni cacciatore: conta solo i danni fatti da quando lo vediamo, non il timer della missione,
/// così chi arriva dopo non viene penalizzato. Se entri nella missione di un altro i suoi danni partono
/// dall'inizio della missione, ma il suo tempo no: quei danni restano nel totale e fuori dal DPS.
/// </summary>
internal sealed class DpsTracker
{
    private sealed class Hunter(int baseline)
    {
        public Stopwatch Since { get; } = Stopwatch.StartNew();
        public int Baseline { get; } = baseline;
        public int Last { get; set; }
    }

    private readonly Dictionary<string, Hunter> _hunters = [];
    private int _questId = -1;

    public void Update(int questId, IEnumerable<(string Name, int Damage, bool IsMe)> rows)
    {
        if (questId != _questId)
            _hunters.Clear();
        _questId = questId;

        foreach (var (name, damage, isMe) in rows)
        {
            // Danni azzerati (altro mostro da solo, rientro): quel cacciatore riparte da zero.
            // I tuoi danni nascono con te, gli altri possono averne già.
            if (!_hunters.TryGetValue(name, out var hunter) || damage < hunter.Last)
                _hunters[name] = hunter = new Hunter(isMe ? 0 : damage);
            hunter.Last = damage;
        }
    }

    public double Dps(string name) =>
        _hunters.TryGetValue(name, out var h) ? (h.Last - h.Baseline) / Math.Max(h.Since.Elapsed.TotalSeconds, 1) : 0;
}
