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

    // Palette della mod: solo bianco, nero e grigi (ABGR, formato di ImDrawList).
    // Mostro nero, altri giocatori bianco, tu grigio; la parte vuota della barra è un grigio chiaro trasparente.
    private const uint MonsterFill = 0xF0000000;
    private const uint OthersFill = 0xF0FFFFFF;
    private const uint MyFill = 0xF0707070;
    private const uint BarTrack = 0x60B0B0B0;
    private const uint BarBorder = 0x90FFFFFF;
    private const uint TextColor = 0xFFFFFFFF;
    private const uint ShadowColor = 0xC0000000;

    private const float FontScale = 1.8f;
    private const float BarWidth = 810;
    private const float Spacing = 8;
    private const int MaxIcons = 3; // al massimo tre debolezze per mostro

    private readonly record struct MonsterInfo(string Name, float Health, float MaxHealth, float? Capture, Element[] Weaknesses);
    private readonly record struct DamageRow(string Name, int Damage, bool IsMe, WeaponType Weapon);

    // Snapshot preparato in OnUpdate e solo letto da OnImGuiFreeRender.
    private MonsterInfo? _target;
    private DamageRow[] _damage = [];

    // Mostro che stai combattendo: l'ultimo grande mostro che ha perso vita.
    private readonly Dictionary<nint, float> _lastHealth = [];
    private nint _targetInstance;
    private readonly Stopwatch _sinceLastHit = new();

    private float _sinceRefresh = RefreshSeconds;
    private bool _versionChecked;
    private bool _partyMemoryEnabled;
    private bool _monsterErrorLogged;
    private bool _partyErrorLogged;

    public PluginData Initialize() => new()
    {
        ImGuiWrappedInTreeNode = false
    };

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

            foreach (var m in monsters)
            {
                var health = m.Health;
                if (_lastHealth.TryGetValue(m.Instance, out var previous) && health < previous)
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
            _target = new MonsterInfo(target.Name, Math.Max(target.Health, 0), target.MaxHealth, capture, data?.Weaknesses ?? []);
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

    private void UpdateDamage()
    {
        if (_target is not { } target)
        {
            _damage = [];
            return;
        }

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

    public void OnImGuiFreeRender()
    {
#if PREVIEW
        // Anteprima con dati finti per provare la grafica fuori dalla caccia: dotnet build -p:DefineConstants=PREVIEW
        _target ??= new MonsterInfo("Rathalos", 7400, 21000, 0.2f, [Element.Dragon, Element.Thunder]);
        if (_damage.Length == 0)
            _damage = [new("Cacciatore 1", 6200, true, WeaponType.LongSword), new("Cacciatore 2", 4900, false, WeaponType.Bow), new("Cacciatore 3", 2500, false, WeaponType.Hammer)];
#endif
        if (_target is not { } target)
            return;

        // In alto a destra: DisplaySize è la risoluzione reale del gioco (non quella scalata da Windows).
        var display = ImGui.GetIO().DisplaySize;
        ImGui.SetNextWindowPos(new Vector2(display.X - 60, 60), ImGuiCond.Always, new Vector2(1, 0));
        ImGui.SetNextWindowBgAlpha(0);
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground
            | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize;
        if (ImGui.Begin("MH World Overlay", flags))
        {
            ImGui.SetWindowFontScale(FontScale);
            var rowHeight = ImGui.GetFontSize() + 14; // margine sopra e sotto: niente lettere tagliate (q, g, p)

            var fraction = target.Health / target.MaxHealth;
            var weaknesses = target.Weaknesses.Select(GameIcons.ForElement).ToArray();
            Row(weaknesses, fraction, rowHeight * 1.2f, MonsterFill,
                $"{target.Name}   {target.Health:0} / {target.MaxHealth:0}  ({fraction:P0})", target.Capture);

            var total = _damage.Sum(d => d.Damage);
            if (total > 0)
            {
                ImGui.Dummy(new Vector2(0, Spacing));
                foreach (var d in _damage.OrderByDescending(d => d.Damage))
                {
                    var share = (float)d.Damage / total;
                    Row([GameIcons.ForWeapon(d.Weapon)], share, rowHeight, d.IsMe ? MyFill : OthersFill, $"{d.Name}   {d.Damage}  ({share:P0})", null);
                }
            }
        }
        ImGui.End();
    }

    // Una riga: icone del gioco a sinistra (debolezze o arma), poi la barra con il testo centrato.
    // Le icone sono allineate a destra in uno spazio fisso, così le barre restano allineate tra loro.
    private static void Row(GameIcons.Icon?[] icons, float fraction, float height, uint fill, string label, float? capture)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var iconSize = ImGui.GetFontSize() * 1.6f;
        var iconArea = (iconSize + Spacing) * MaxIcons;

        var x = origin.X + iconArea - icons.Length * (iconSize + Spacing);
        foreach (var icon in icons)
        {
            if (icon is { } i)
            {
                var min = new Vector2(x, origin.Y + (height - iconSize) / 2);
                drawList.AddImage(i.Texture, min, min + new Vector2(iconSize, iconSize), i.Uv0, i.Uv1);
            }
            x += iconSize + Spacing;
        }

        var barMin = new Vector2(origin.X + iconArea, origin.Y);
        var barMax = barMin + new Vector2(BarWidth, height);
        drawList.AddRectFilled(barMin, barMax, BarTrack, 4);
        if (fraction > 0)
            drawList.AddRectFilled(barMin, new Vector2(barMin.X + BarWidth * Math.Clamp(fraction, 0, 1), barMax.Y), fill, 4);
        drawList.AddRect(barMin, barMax, BarBorder, 4, ImDrawFlags.None, 1.5f);

        // Segno "/" alla soglia di cattura (il momento del teschio).
        if (capture is { } c)
        {
            var cx = barMin.X + BarWidth * c;
            var bottom = new Vector2(cx - 7, barMax.Y + 3);
            var top = new Vector2(cx + 7, barMin.Y - 3);
            drawList.AddLine(bottom + new Vector2(1.5f, 1.5f), top + new Vector2(1.5f, 1.5f), ShadowColor, 5);
            drawList.AddLine(bottom, top, TextColor, 3);
        }

        CenteredText(drawList, barMin, barMax, label);
        ImGui.Dummy(new Vector2(iconArea + BarWidth, height));
        ImGui.Dummy(new Vector2(0, Spacing / 2));
    }

    // Testo bianco con contorno nero: si legge su barre nere, grigie e bianche.
    private static void CenteredText(ImDrawListPtr drawList, Vector2 min, Vector2 max, string text)
    {
        var size = ImGui.CalcTextSize(text);
        var pos = new Vector2(min.X + (max.X - min.X - size.X) / 2, min.Y + (max.Y - min.Y - size.Y) / 2);
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize();
        foreach (var offset in new[] { new Vector2(-2, 0), new Vector2(2, 0), new Vector2(0, -2), new Vector2(0, 2) })
            drawList.AddText(font, fontSize, pos + offset, ShadowColor, text);
        drawList.AddText(font, fontSize, pos, TextColor, text);
    }
}
