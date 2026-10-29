using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using SharpPluginLoader.Core;
using SharpPluginLoader.Core.Entities;

namespace MHWorldOverlay;

public sealed class OverlayPlugin : IPlugin
{
    public string Name => "MH World Overlay";
    public string Author => "Ohhvalvaa";

    private const float RefreshSeconds = 0.25f;
    // Dopo quanti secondi senza colpi il mostro sparisce dal pannello (anche se morto o catturato).
    private const double TargetTimeoutSeconds = 30;

    // Piccoli mostri ed elementi d'ambiente: non vanno nel pannello.
    private static readonly HashSet<MonsterType> SmallMonsters =
    [
        MonsterType.Aptonoth, MonsterType.Jagras, MonsterType.Mosswine, MonsterType.Gajau,
        MonsterType.KestodonM, MonsterType.KestodonF, MonsterType.Apceros, MonsterType.KelbiM,
        MonsterType.KelbiF, MonsterType.Hornetaur, MonsterType.Vespoid, MonsterType.Mernos,
        MonsterType.Raphinos, MonsterType.Shamos, MonsterType.Barnos, MonsterType.Girros,
        MonsterType.Gastodon, MonsterType.Noios, MonsterType.Magmacore, MonsterType.Magmacore2,
        MonsterType.Gajalaka, MonsterType.SmallBarrel, MonsterType.LargeBarrel,
        MonsterType.TrainingPole, MonsterType.TrainingWagon, MonsterType.Popo, MonsterType.Anteka,
        MonsterType.Wulg, MonsterType.Cortos, MonsterType.Boaboa, MonsterType.Unavaliable,
    ];

    // Barre nello stile dell'HUD del gioco: riempimento sfumato (chiaro sopra, scuro sotto) dentro una
    // cornice nera con un filo metallico. Palette della mod: solo bianco, nero e grigi.
    // Mostro nero, altri giocatori bianco, tu grigio; la parte vuota della barra è un grigio chiaro trasparente.
    private readonly record struct BarColors(uint Top, uint Bottom);
    private static readonly BarColors MonsterFill = new(Rgba(64, 64, 64), Rgba(0, 0, 0));
    private static readonly BarColors MyFill = new(Rgba(150, 150, 150), Rgba(92, 92, 92));
    private static readonly BarColors OthersFill = new(Rgba(255, 255, 255), Rgba(196, 196, 196));
    private static readonly uint RecentDamage = Rgba(255, 255, 255, 170); // vita appena persa: si svuota piano, come nel gioco
    private static readonly uint BarTrack = Rgba(176, 176, 176, 96);
    private static readonly uint FrameOuter = Rgba(0, 0, 0, 230);
    private static readonly uint FrameInner = Rgba(150, 156, 150, 220);
    private static readonly uint Gloss = Rgba(255, 255, 255, 70);
    private static readonly uint TextColor = Rgba(255, 255, 255);
    private static readonly uint ShadowColor = Rgba(0, 0, 0, 200);

    // Misure pensate per uno schermo alto 2160 pixel (4K): a risoluzioni più basse si riducono in proporzione.
    private const float ReferenceHeight = 2160;
    private const float FontScale = 1.8f;
    private const float BarWidth = 810;
    private const float Spacing = 8;
    private const int MaxIcons = 3; // al massimo tre debolezze per mostro

    // La parte rossa resta ferma un attimo dopo il colpo, poi scende.
    private const float RecentHoldSeconds = 0.8f;
    private const float RecentDrainPerSecond = 0.25f;

    private readonly record struct MonsterInfo(nint Instance, string Name, float Health, float MaxHealth, float? Capture, Element[] Weaknesses);
    private readonly record struct DamageRow(string Name, int Damage, bool IsMe, WeaponType Weapon);

    // Snapshot preparato in OnUpdate e solo letto da OnImGuiFreeRender.
    private MonsterInfo? _target;
    private DamageRow[] _damage = [];

    // Mostro che stai combattendo: l'ultimo grande mostro che ha perso vita.
    // Se è l'obiettivo della missione, un altro mostro colpito lì vicino non gli ruba il pannello.
    private readonly Dictionary<nint, float> _lastHealth = [];
    private nint _targetInstance;
    private readonly Stopwatch _sinceLastHit = new();

    // DPS: per ogni cacciatore il tempo da quando è entrato nella missione (non il timer della missione),
    // così chi arriva dopo non viene penalizzato.
    private readonly Dictionary<string, Stopwatch> _joined = [];
    private int _lastQuestId = -1;
    private int _lastTotal;

    // Parte rossa della barra del mostro.
    private nint _recentInstance;
    private float _recent;
    private float _lastFraction;
    private float _recentHold;

    private Settings _settings = new();
    private float _scale = 1;

    private float _sinceRefresh = RefreshSeconds;
    private bool _versionChecked;
    private bool _partyMemoryEnabled;
    private bool _monsterErrorLogged;
    private bool _partyErrorLogged;

    public PluginData Initialize() => new()
    {
        ImGuiWrappedInTreeNode = false
    };

    public void OnLoad() => _settings = Settings.Load();

    public void OnUpdate(float deltaTime)
    {
        _sinceRefresh += deltaTime;
        if (_sinceRefresh < RefreshSeconds)
            return;
        _sinceRefresh = 0;

        CheckVersionOnce();
        UpdateTarget();
        UpdateDamage();
    }

    // Il titolo della finestra contiene la versione, ma esiste solo dopo l'avvio: si controlla qui e non in OnLoad.
    private void CheckVersionOnce()
    {
        if (_versionChecked)
            return;
        _partyMemoryEnabled = GameMemory.IsSupportedVersion(out var title);
        if (string.IsNullOrEmpty(title))
            return;
        _versionChecked = true;
        Log.Info(_partyMemoryEnabled
            ? $"[MH World Overlay] Versione {GameMemory.SupportedVersion}: danni del gruppo attivi"
            : $"[MH World Overlay] Titolo '{title}': versione diversa da {GameMemory.SupportedVersion}, danni del gruppo spenti");
    }

    private void UpdateTarget()
    {
        try
        {
            var monsters = Monster.GetAllMonsters()
                .Where(m => !SmallMonsters.Contains(m.Type) && m.MaxHealth > 0)
                .ToArray();

            var objectives = QuestObjectives();
            var current = monsters.FirstOrDefault(m => m.Instance == _targetInstance);
            var currentLocked = current is not null && objectives.Contains(current.Type) && current.Health > 0
                && _sinceLastHit.Elapsed.TotalSeconds < TargetTimeoutSeconds;

            foreach (var m in monsters)
            {
                var health = m.Health;
                if (_lastHealth.TryGetValue(m.Instance, out var previous) && health < previous
                    && (m.Instance == _targetInstance || !currentLocked || objectives.Contains(m.Type)))
                {
                    _targetInstance = m.Instance;
                    _sinceLastHit.Restart();
                }
                _lastHealth[m.Instance] = health;
            }

            // Mostri spariti (fine missione, cambio zona): dimenticali.
            foreach (var gone in _lastHealth.Keys.Except(monsters.Select(m => m.Instance)).ToArray())
                _lastHealth.Remove(gone);

            var target = monsters.FirstOrDefault(m => m.Instance == _targetInstance);
            if (target is null || _sinceLastHit.Elapsed.TotalSeconds >= TargetTimeoutSeconds)
            {
                _target = null;
                return;
            }
            var data = MonsterData.For((uint)target.Type);
            var capture = data?.Capture > 0 ? data.Value.Capture : (float?)null;
            _target = new MonsterInfo(target.Instance, target.Name, Math.Max(target.Health, 0), target.MaxHealth, capture, data?.Weaknesses ?? []);
        }
        catch (Exception e)
        {
            _target = null;
            if (!_monsterErrorLogged)
            {
                Log.Error($"[MH World Overlay] Lettura mostri fallita: {e}");
                _monsterErrorLogged = true;
            }
        }
    }

    // Mostri ancora da cacciare, catturare o uccidere nella missione; vuoto fuori missione o in esplorazione.
    private static HashSet<MonsterType> QuestObjectives()
    {
        var types = new HashSet<MonsterType>();
        try
        {
            if (Quest.CurrentQuestId < 0)
                return types;
            foreach (var o in Quest.Objectives)
            {
                if (((int)o.Type & (int)QuestTargetType.Monster) != 0 && o.Count < o.RequiredCount)
                    types.Add((MonsterType)o.Id);
            }
        }
        catch
        {
            types.Clear();
        }
        return types;
    }

    private void UpdateDamage()
    {
        if (_target is not { } target)
        {
            _damage = [];
            return;
        }
        ReadDamage(target);
        TrackJoins();
    }

    private void TrackJoins()
    {
        // Nuova missione, o danni azzerati (altro mostro da solo, rientro): si riparte da zero.
        var questId = CurrentQuestId();
        var total = _damage.Sum(d => d.Damage);
        if (questId != _lastQuestId || total < _lastTotal)
            _joined.Clear();
        _lastQuestId = questId;
        _lastTotal = total;

        foreach (var d in _damage)
            if (!_joined.ContainsKey(d.Name))
                _joined[d.Name] = Stopwatch.StartNew();
    }

    private static int CurrentQuestId()
    {
        try
        {
            return Quest.CurrentQuestId;
        }
        catch
        {
            return -1;
        }
    }

    private double Dps(DamageRow d) =>
        _joined.TryGetValue(d.Name, out var since) ? d.Damage / Math.Max(since.Elapsed.TotalSeconds, 1) : 0;

    private void ReadDamage(MonsterInfo target)
    {
        if (_partyMemoryEnabled)
        {
            try
            {
                var party = GameMemory.ReadParty();
                if (party.Length > 0)
                {
                    var myWeapon = MyWeapon();
                    _damage = party
                        .Select(p => new DamageRow(p.Name, p.Damage, p.IsMe, p.IsMe && myWeapon != WeaponType.None ? myWeapon : p.Weapon))
                        .ToArray();
                    return;
                }
            }
            catch (Exception e)
            {
                _partyMemoryEnabled = false;
                if (!_partyErrorLogged)
                {
                    Log.Error($"[MH World Overlay] Lettura danni del gruppo fallita, la spengo: {e}");
                    _partyErrorLogged = true;
                }
            }
        }

        // Da solo: i danni sono la vita persa dal mostro (compresi Felyne e ambiente).
        _damage = [new DamageRow("Tu", (int)(target.MaxHealth - target.Health), true, MyWeapon())];
    }

    // Arma del giocatore locale da SPL: più aggiornata del dato del gruppo quando cambi arma.
    private static WeaponType MyWeapon()
    {
        try
        {
            return Player.MainPlayer?.CurrentWeaponType ?? WeaponType.None;
        }
        catch
        {
            return WeaponType.None;
        }
    }

    // Impostazioni nel menu di SharpPluginLoader (F9); ogni modifica viene salvata subito.
    public void OnImGuiRender()
    {
        var changed = false;

        var showDamage = _settings.ShowPartyDamage;
        if (ImGui.Checkbox("Show party damage meter", ref showDamage))
        {
            _settings.ShowPartyDamage = showDamage;
            changed = true;
        }

        var display = (int)_settings.DamageDisplay;
        if (ImGui.Combo("Damage meter shows", ref display, ["Damage", "DPS", "Damage + DPS"], 3))
        {
            _settings.DamageDisplay = (DamageDisplay)display;
            changed = true;
        }

        var scale = _settings.Scale;
        if (ImGui.SliderFloat("Size", ref scale, 0.5f, 2f, "%.2fx"))
            _settings.Scale = scale;
        // Il cursore si salva quando lo lasci, non a ogni pixel trascinato.
        changed |= ImGui.IsItemDeactivatedAfterEdit();
        ImGui.TextDisabled("Size is relative to your screen height (1.00x = default).");

        if (changed)
            _settings.Save();
    }

    public void OnImGuiFreeRender()
    {
#if PREVIEW
        // Anteprima con dati finti per provare la grafica fuori dalla caccia: dotnet build -p:DefineConstants=PREVIEW
        // La vita scende di un colpo ogni secondo per vedere la parte rossa.
        _target = new MonsterInfo(1, "Rathalos", 9400 - Environment.TickCount64 / 1000 % 5 * 500, 21000, 0.2f, [Element.Dragon, Element.Thunder]);
        if (_damage.Length == 0)
        {
            _damage = [new("Cacciatore 1", 6200, true, WeaponType.LongSword), new("Cacciatore 2", 4900, false, WeaponType.Bow), new("Cacciatore 3", 2500, false, WeaponType.Hammer)];
            TrackJoins();
        }
#endif
        if (_target is not { } target)
            return;

        // DisplaySize è la risoluzione reale del gioco (non quella scalata da Windows).
        var io = ImGui.GetIO();
        var screen = io.DisplaySize;
        _scale = screen.Y / ReferenceHeight * Math.Clamp(_settings.Scale, 0.5f, 2f);

        var fraction = Math.Clamp(target.Health / target.MaxHealth, 0, 1);
        UpdateRecentDamage(target.Instance, fraction, io.DeltaTime);

        // In alto a destra.
        ImGui.SetNextWindowPos(new Vector2(screen.X - 60 * _scale, 60 * _scale), ImGuiCond.Always, new Vector2(1, 0));
        ImGui.SetNextWindowBgAlpha(0);
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground
            | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize;
        if (ImGui.Begin("MH World Overlay", flags))
        {
            ImGui.SetWindowFontScale(FontScale * _scale);
            var fontSize = ImGui.GetFontSize();

            var weaknesses = target.Weaknesses.Select(GameIcons.ForElement).ToArray();
            Row(weaknesses, target.Name, $"{target.Health:0} / {target.MaxHealth:0}  ({fraction:P0})",
                fraction, _recent, fontSize * 0.8f, MonsterFill, target.Capture);

            var total = _damage.Sum(d => d.Damage);
            if (_settings.ShowPartyDamage && total > 0)
            {
                ImGui.Dummy(new Vector2(0, Spacing * _scale));
                foreach (var d in _damage.OrderByDescending(d => d.Damage))
                {
                    var share = (float)d.Damage / total;
                    Row([GameIcons.ForWeapon(d.Weapon)], d.Name, $"{DamageText(d)}  ({share:P0})",
                        share, share, fontSize * 0.55f, d.IsMe ? MyFill : OthersFill, null);
                }
            }
        }
        ImGui.End();
    }

    private string DamageText(DamageRow d) => _settings.DamageDisplay switch
    {
        DamageDisplay.Dps => FormattableString.Invariant($"{Dps(d):0.0} DPS"),
        DamageDisplay.Both => FormattableString.Invariant($"{d.Damage}   {Dps(d):0.0} DPS"),
        _ => $"{d.Damage}",
    };

    // Come nel gioco: dopo un colpo la vita persa resta rossa per un attimo e poi si svuota.
    private void UpdateRecentDamage(nint instance, float fraction, float deltaTime)
    {
        if (instance != _recentInstance || fraction > _recent)
        {
            _recentInstance = instance;
            _recent = fraction;
            _recentHold = 0;
        }
        else if (fraction < _lastFraction)
        {
            _recentHold = RecentHoldSeconds;
        }
        else if (_recentHold > 0)
        {
            _recentHold -= deltaTime;
        }
        else
        {
            _recent = Math.Max(fraction, _recent - RecentDrainPerSecond * deltaTime);
        }
        _lastFraction = fraction;
    }

    // Una riga: icone del gioco a sinistra (debolezze o arma), sopra la barra il nome a sinistra e i numeri a destra.
    // Le icone sono allineate a destra in uno spazio fisso, così le barre restano allineate tra loro.
    private void Row(GameIcons.Icon?[] icons, string name, string values, float fraction, float recent,
        float barHeight, BarColors fill, float? capture)
    {
        var s = _scale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var fontSize = ImGui.GetFontSize();
        var width = BarWidth * s;
        var textGap = 4 * s;
        var rowHeight = fontSize + textGap + barHeight;

        var iconSize = fontSize * 1.6f;
        var gap = Spacing * s;
        var iconArea = (iconSize + gap) * MaxIcons;
        var x = origin.X + iconArea - icons.Length * (iconSize + gap);
        foreach (var icon in icons)
        {
            if (icon is { } i)
            {
                var min = new Vector2(x, origin.Y + (rowHeight - iconSize) / 2);
                drawList.AddImage(i.Texture, min, min + new Vector2(iconSize, iconSize), i.Uv0, i.Uv1);
            }
            x += iconSize + gap;
        }

        var left = origin.X + iconArea + gap / 2;
        OutlinedText(drawList, new Vector2(left + 2 * s, origin.Y), name);
        OutlinedText(drawList, new Vector2(left + width - ImGui.CalcTextSize(values).X - 2 * s, origin.Y), values);

        var barMin = new Vector2(left, origin.Y + fontSize + textGap);
        var barMax = barMin + new Vector2(width, barHeight);
        DrawBar(drawList, barMin, barMax, fraction, recent, fill);

        // Segno "/" alla soglia di cattura (il momento del teschio).
        if (capture is { } c)
        {
            var cx = barMin.X + width * c;
            var bottom = new Vector2(cx - 5 * s, barMax.Y + 4 * s);
            var top = new Vector2(cx + 5 * s, barMin.Y - 4 * s);
            var shadow = new Vector2(1.5f, 1.5f) * s;
            drawList.AddLine(bottom + shadow, top + shadow, ShadowColor, 5 * s);
            drawList.AddLine(bottom, top, TextColor, 3 * s);
        }

        ImGui.Dummy(new Vector2(iconArea + gap / 2 + width + 4 * s, rowHeight + 4 * s));
        ImGui.Dummy(new Vector2(0, gap / 2));
    }

    // Barra alla maniera di vita e stamina del gioco: cornice nera con filo metallico, fondo scuro,
    // riempimento sfumato con un riflesso nella metà alta e una punta chiara dove finisce.
    private void DrawBar(ImDrawListPtr drawList, Vector2 min, Vector2 max, float fraction, float recent, BarColors fill)
    {
        var s = _scale;
        var outer = 3 * s;
        var inner = 1.5f * s;
        // Solo il bordo: dentro la barra il fondo resta trasparente.
        drawList.AddRect(min - new Vector2(inner + outer / 2), max + new Vector2(inner + outer / 2), FrameOuter, 0, ImDrawFlags.None, outer);
        drawList.AddRect(min - new Vector2(inner), max + new Vector2(inner), FrameInner, 0, ImDrawFlags.None, Math.Max(inner, 1));
        drawList.AddRectFilled(min, max, BarTrack);

        var width = max.X - min.X;
        var fillEnd = min.X + width * Math.Clamp(fraction, 0, 1);
        var recentEnd = min.X + width * Math.Clamp(recent, 0, 1);
        if (recentEnd > fillEnd)
            drawList.AddRectFilled(new Vector2(fillEnd, min.Y), new Vector2(recentEnd, max.Y), RecentDamage);

        if (fillEnd <= min.X)
            return;
        var fillMax = new Vector2(fillEnd, max.Y);
        drawList.AddRectFilledMultiColor(min, fillMax, fill.Top, fill.Top, fill.Bottom, fill.Bottom);
        var glossBottom = min.Y + (max.Y - min.Y) * 0.4f;
        drawList.AddRectFilled(min, new Vector2(fillEnd, glossBottom), Gloss);
        if (fraction < 1)
            drawList.AddLine(new Vector2(fillEnd, min.Y), fillMax, Gloss | 0xFF000000, Math.Max(1.5f * s, 1));
    }

    // Testo bianco con contorno nero, come le scritte dell'HUD del gioco.
    private void OutlinedText(ImDrawListPtr drawList, Vector2 pos, string text)
    {
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize();
        var o = Math.Max(2 * _scale, 1);
        foreach (var offset in new[] { new Vector2(-o, 0), new Vector2(o, 0), new Vector2(0, -o), new Vector2(0, o) })
            drawList.AddText(font, fontSize, pos + offset, ShadowColor, text);
        drawList.AddText(font, fontSize, pos, TextColor, text);
    }

    // Colore nel formato di ImDrawList (ABGR).
    private static uint Rgba(byte r, byte g, byte b, byte a = 255) => (uint)(a << 24 | b << 16 | g << 8 | r);
}
