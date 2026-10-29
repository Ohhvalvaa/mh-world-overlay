using System.Text.Json;
using System.Text.Json.Serialization;
using SharpPluginLoader.Core;

namespace MHWorldOverlay;

public enum DamageDisplay { Damage, Dps, Both }

/// <summary>
/// Opzioni modificabili dal menu di SharpPluginLoader (F9) e salvate accanto alla DLL,
/// in nativePC\plugins\CSharp\MHWorldOverlay\MHWorldOverlay.json.
/// </summary>
internal sealed class Settings
{
    // Percorso relativo alla cartella del gioco: SPL può caricare la DLL da memoria, quindi Assembly.Location non è affidabile.
    private const string FilePath = @"nativePC\plugins\CSharp\MHWorldOverlay\MHWorldOverlay.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public bool ShowPartyDamage { get; set; } = true;
    public DamageDisplay DamageDisplay { get; set; } = DamageDisplay.Damage;
    // Moltiplicatore sopra la scala automatica (che segue l'altezza dello schermo, riferimento 4K).
    public float Scale { get; set; } = 1.0f;

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings();
        }
        catch (Exception e)
        {
            Log.Error($"[MH World Overlay] Impostazioni illeggibili, uso quelle predefinite: {e.Message}");
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception e)
        {
            Log.Error($"[MH World Overlay] Salvataggio impostazioni fallito: {e.Message}");
        }
    }
}
