using System.Numerics;
using SharpPluginLoader.Core;
using SharpPluginLoader.Core.Entities;
using SharpPluginLoader.Core.Rendering;
using SharpPluginLoader.Core.Resources;

namespace MHWorldOverlay;

/// <summary>
/// Icone prese dagli atlanti dell'interfaccia del gioco (caricati in memoria, niente file estratti).
/// Ogni icona è un riquadro di 64x64 pixel; le posizioni sono state lette dagli atlanti a schermo.
/// </summary>
internal static class GameIcons
{
    public readonly record struct Icon(TextureHandle Texture, Vector2 Uv0, Vector2 Uv1);

    private const string ItemAtlas = @"ui\common\tex\cmn_item00_ID";     // 1024x1024, armi nell'ultima riga
    private const string StatusAtlas = @"ui\common\tex\cmn_status00_ID"; // 512x1024, elementi nella prima riga
    private const float Cell = 64;

    private static readonly Dictionary<WeaponType, int> WeaponColumns = new()
    {
        [WeaponType.SwordAndShield] = 0, [WeaponType.GreatSword] = 1, [WeaponType.LongSword] = 2,
        [WeaponType.DualBlades] = 3, [WeaponType.Lance] = 4, [WeaponType.GunLance] = 5,
        [WeaponType.Hammer] = 6, [WeaponType.HuntingHorn] = 7, [WeaponType.SwitchAxe] = 8,
        [WeaponType.ChargeBlade] = 9, [WeaponType.InsectGlaive] = 10, [WeaponType.LightBowgun] = 11,
        [WeaponType.HeavyBowgun] = 12, [WeaponType.Bow] = 13,
    };

    private static readonly Dictionary<Element, int> ElementColumns = new()
    {
        [Element.Fire] = 3, [Element.Water] = 4, [Element.Thunder] = 5, [Element.Ice] = 6, [Element.Dragon] = 7,
    };

    private static (TextureHandle Handle, uint Width, uint Height)? _items;
    private static (TextureHandle Handle, uint Width, uint Height)? _status;

    public static Icon? ForWeapon(WeaponType weapon) =>
        WeaponColumns.TryGetValue(weapon, out var column) && Load(ItemAtlas, ref _items) is { } atlas
            ? FromCell(atlas, column, (int)(atlas.Height / Cell) - 1)
            : null;

    public static Icon? ForElement(Element element) =>
        ElementColumns.TryGetValue(element, out var column) && Load(StatusAtlas, ref _status) is { } atlas
            ? FromCell(atlas, column, 0)
            : null;

    private static Icon FromCell((TextureHandle Handle, uint Width, uint Height) atlas, int column, int row) =>
        new(atlas.Handle,
            new Vector2(column * Cell / atlas.Width, row * Cell / atlas.Height),
            new Vector2((column + 1) * Cell / atlas.Width, (row + 1) * Cell / atlas.Height));

    // Va chiamato dal thread di disegno (OnImGuiFreeRender). Un tentativo fallito non viene ripetuto.
    private static (TextureHandle Handle, uint Width, uint Height)? Load(string path, ref (TextureHandle, uint, uint)? cache)
    {
        if (cache is null)
        {
            cache = (TextureHandle.Invalid, 0, 0);
            try
            {
                var texture = ResourceManager.GetResource<Texture>(path, MtDti.Find("rTexture")!);
                if (texture is not null && texture.Width > 0)
                    cache = (texture.GetTextureHandle(), texture.Width, texture.Height);
                else
                    Log.Error($"[MH World Overlay] Atlante icone non trovato: {path}");
            }
            catch (Exception e)
            {
                Log.Error($"[MH World Overlay] Caricamento atlante {path} fallito: {e}");
            }
        }
        return cache.Value.Item1 != TextureHandle.Invalid ? cache : null;
    }
}
