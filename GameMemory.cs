using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SharpPluginLoader.Core.Entities;

namespace MHWorldOverlay;

/// <summary>
/// Letture di memoria del gioco per i dati che SharpPluginLoader non espone (danni per giocatore).
/// Indirizzi e catene di puntatori presi dalla mappa di HunterPie per la versione 421810
/// (HunterPie/Address/MonsterHunterWorld.421810.map). Ogni indirizzo viene controllato con
/// VirtualQuery prima di leggerlo: una lettura sbagliata dentro il processo farebbe crashare il gioco.
/// </summary>
internal static class GameMemory
{
    public const string SupportedVersion = "421810";

    private const nint SessionAddress = 0x051C46B8;
    private static readonly int[] SessionPartyOffsets = [0x258, 0x10, 0x6574];
    private const nint PartyAddress = 0x05013530;
    private static readonly int[] PartyOffsets = [0x1AB0];
    private const nint DamageAddress = 0x051C46B8;
    private static readonly int[] DamageOffsets = [0x258, 0x38, 0x450, 0x8, 0x48];
    private const nint LevelAddress = 0x05013950;
    private static readonly int[] LevelOffsets = [0xA8];

    private const int PartySlots = 4;
    private const int PartyMemberStride = 0x58; // MHWPartyMemberStructure di HunterPie
    private const int DamageStride = 0x2A0;
    private const nint PlayerSaveStride = 0x26CC00;

    private static readonly nint Base = Process.GetCurrentProcess().MainModule!.BaseAddress;

    public readonly record struct PartyMember(string Name, int Damage, bool IsMe, WeaponType Weapon);

    /// <summary>True se la versione del gioco (nel titolo della finestra) è quella degli indirizzi.</summary>
    public static bool IsSupportedVersion(out string windowTitle)
    {
        windowTitle = Process.GetCurrentProcess().MainWindowTitle;
        return windowTitle.Contains(SupportedVersion);
    }

    /// <summary>Componenti del gruppo con i danni della missione; vuoto se sei da solo o se i dati non ci sono.</summary>
    public static PartyMember[] ReadParty()
    {
        var sessionPtr = Follow(Base + SessionAddress, SessionPartyOffsets);
        if (sessionPtr == 0 || !TryRead(sessionPtr, out int partySize) || partySize <= 0)
            return [];

        var partyPtr = Follow(Base + PartyAddress, PartyOffsets);
        var damagePtr = Follow(Base + DamageAddress, DamageOffsets);
        if (partyPtr == 0 || damagePtr == 0)
            return [];

        var myName = ReadMyName();
        var members = new List<PartyMember>(PartySlots);
        for (var i = 0; i < PartySlots; i++)
        {
            if (!TryRead(partyPtr + i * PartyMemberStride, out nint memberPtr) || memberPtr == 0)
                continue;
            var name = ReadString(memberPtr + 0x49, 32);
            if (string.IsNullOrEmpty(name) || !TryRead(damagePtr + i * DamageStride, out int damage))
                continue;
            var weapon = TryRead(memberPtr + 0x7C, out byte weaponId) ? (WeaponType)weaponId : WeaponType.None;
            members.Add(new PartyMember(name, Math.Max(damage, 0), name == myName, weapon));
        }
        return members.ToArray();
    }

    private static string? ReadMyName()
    {
        var firstSave = Follow(Base + LevelAddress, LevelOffsets);
        if (firstSave == 0 || !TryRead(firstSave + 0x44, out uint slot) || !TryRead(firstSave, out nint saves) || saves == 0)
            return null;
        return ReadString(saves + PlayerSaveStride * (nint)slot + 0x50, 32);
    }

    // Stessa semantica di HunterPie ReadAsync(address, offsets): per ogni offset, address = *address + offset.
    private static nint Follow(nint address, int[] offsets)
    {
        foreach (var offset in offsets)
        {
            if (!TryRead(address, out nint next) || next == 0)
                return 0;
            address = next + offset;
        }
        return address;
    }

    private static bool TryRead<T>(nint address, out T value) where T : unmanaged
    {
        value = default;
        if (!IsReadable(address, Marshal.SizeOf<T>()))
            return false;
        value = Marshal.PtrToStructure<T>(address);
        return true;
    }

    private static string? ReadString(nint address, int maxLength)
    {
        if (!IsReadable(address, maxLength))
            return null;
        var bytes = new byte[maxLength];
        Marshal.Copy(address, bytes, 0, maxLength);
        var end = Array.IndexOf(bytes, (byte)0);
        return Encoding.UTF8.GetString(bytes, 0, end < 0 ? maxLength : end);
    }

    private static bool IsReadable(nint address, int size)
    {
        if (address < 0x10000)
            return false;
        if (VirtualQuery(address, out var info, (nuint)Marshal.SizeOf<MemoryBasicInformation>()) == 0)
            return false;
        const uint memCommit = 0x1000;
        const uint readable = 0x02 | 0x04 | 0x08 | 0x20 | 0x40 | 0x80; // READONLY, READWRITE, WRITECOPY, EXECUTE_*
        const uint guardOrNoAccess = 0x100 | 0x01;
        if (info.State != memCommit || (info.Protect & readable) == 0 || (info.Protect & guardOrNoAccess) != 0)
            return false;
        return address + size <= info.BaseAddress + (nint)info.RegionSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public nint BaseAddress;
        public nint AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public nuint RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll")]
    private static extern nuint VirtualQuery(nint address, out MemoryBasicInformation buffer, nuint length);
}
