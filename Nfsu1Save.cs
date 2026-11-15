// Lettura/scrittura dei salvataggi .ugd di Need for Speed: Underground (NFS7).
// Formato ricostruito per analisi statica di "NFS7: Underground Savegame Editor v1.1" di mift0 (2004).
// Verificato per emulazione dell'originale: hash (Adler-32) e lettura uint32 little-endian.
// NON verificato su un salvataggio reale (non ne avevo uno): vedi NFSU1_ugd_formato.md.
using System.Buffers.Binary;
using System.Text;

namespace Nfsu1;

public enum Nfsu1Stat
{
    CircuitStarts, CircuitWins, CircuitStylePoints,
    SprintStarts, SprintWins, SprintStylePoints,
    DriftStarts, DriftWins, DriftStylePoints,
    DragStarts, DragWins, DragStylePoints, DragTimesTotaled,
    TotalStarts, TotalWins, TotalLosses, TotalStylePoints,
}

/// <summary>Categorie di upgrade, nell'ordine in cui compaiono nel file (indice 1..9).</summary>
public enum Nfsu1Upgrade { WeightReduction = 1, Suspension, Engine, Turbo, Nos, Ecu, Transmission, Tires, Brakes }

public sealed class Nfsu1Save
{
    public const int FileSize = 0xF693;          // 63123 byte, esatti
    public const int HashStart = 0x2F;           // l'hash copre [0x2F, 0xF68F)
    public const int HashOffset = 0xF68F;        // uint32 LE, ultimi 4 byte del file
    public const int NameOffset = 0x04;          // stringa terminata da 0 (fino a 0x15)
    public const int DateOffset = 0x16;          // stringa terminata da 0
    public const int MoneyOffset = 0x767B;       // int32 LE, 0..2147483647
    public const int EventTableOffset = 0x6653;  // 0x72 byte: 0, 1 = disponibile, 2 = completato
    public const int EventTableLength = 0x72;
    public const int EventEntries = 0x71;        // indici 0..0x70 contati dall'editor originale
    public const int UpgradeLevelBase = 0x65E3;  // + 4 * categoria (1..9): livello 0..3
    public const int UpgradeUniqueBase = 0x660F; // + 4 * categoria (1..9): 0/1
    public const int UniqueCarsStart = 0xF60F;   // 128 byte a 1 = "sblocca tutte le auto unique"
    public const int UniqueCarsEnd = 0xF68F;

    private static readonly int[] StatOffsets =
    {
        0xF5F3, 0xF5F7, 0xF5FB,             // Circuit: starts, wins, style points
        0xF5CF, 0xF5D3, 0xF5D7,             // Sprint
        0xF5E7, 0xF5EB, 0xF5EF,             // Drift
        0xF5DB, 0xF5DF, 0xF5E3, 0xF60B,     // Drag (+ times totaled)
        0xF5C3, 0xF5C7, 0xF5CB, 0x0033,     // totali: starts, wins, losses, style points
    };

    public byte[] Data { get; }

    private Nfsu1Save(byte[] data) => Data = data;

    // ───── apertura / validazione ─────

    public static Nfsu1Save FromBytes(byte[] data)
    {
        if (data.Length != FileSize) throw new InvalidDataException($"Dimensione {data.Length}, attesi {FileSize}.");
        var s = new Nfsu1Save((byte[])data.Clone());
        if (!s.HashValid) throw new InvalidDataException("Hash non valido: non e' un salvataggio NFS7 (o e' stato modificato senza ricalcolarlo).");
        return s;
    }

    public static Nfsu1Save Load(string path) => FromBytes(File.ReadAllBytes(path));

    /// <summary>Adler-32 (modulo 65521, a=1, b=0) dei byte [0x2F, 0xF68F): e' la funzione nfs7hash dell'originale.</summary>
    public static uint ComputeHash(byte[] d)
    {
        uint a = 1, b = 0;
        for (int i = HashStart; i < HashOffset; i++)
        {
            a = (a + d[i]) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    public uint StoredHash => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(HashOffset, 4));
    public bool HashValid => StoredHash == ComputeHash(Data);
    public void RecalculateHash() => BinaryPrimitives.WriteUInt32LittleEndian(Data.AsSpan(HashOffset, 4), ComputeHash(Data));

    // ───── profilo ─────

    private string ReadCString(int offset, int maxLen)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < maxLen && Data[offset + i] != 0; i++) sb.Append((char)Data[offset + i]);
        return sb.ToString();
    }

    /// <summary>Sola lettura: l'editor originale non scrive mai il nome.</summary>
    public string Name => ReadCString(NameOffset, DateOffset - NameOffset);
    public string Date => ReadCString(DateOffset, HashStart - DateOffset);

    public int Money
    {
        get => BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(MoneyOffset, 4));
        set => BinaryPrimitives.WriteInt32LittleEndian(Data.AsSpan(MoneyOffset, 4), Check(value));
    }

    private static int Check(int v) => v >= 0 ? v : throw new ArgumentOutOfRangeException(nameof(v), "Atteso 0..2147483647.");

    public int GetStat(Nfsu1Stat s) => BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(StatOffsets[(int)s], 4));
    public void SetStat(Nfsu1Stat s, int value) => BinaryPrimitives.WriteInt32LittleEndian(Data.AsSpan(StatOffsets[(int)s], 4), Check(value));
    public static int StatOffset(Nfsu1Stat s) => StatOffsets[(int)s];

    // ───── upgrade dell'auto ─────

    /// <summary>Livello 0..3 (S, 1, 2, 3).</summary>
    public int GetUpgradeLevel(Nfsu1Upgrade u) => Data[UpgradeLevelBase + 4 * (int)u];

    public void SetUpgradeLevel(Nfsu1Upgrade u, int level)
    {
        if (level is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(level));
        Data[UpgradeLevelBase + 4 * (int)u] = (byte)level;
    }

    public bool GetUpgradeUnique(Nfsu1Upgrade u) => Data[UpgradeUniqueBase + 4 * (int)u] != 0;
    public void SetUpgradeUnique(Nfsu1Upgrade u, bool on) => Data[UpgradeUniqueBase + 4 * (int)u] = on ? (byte)1 : (byte)0;

    // ───── auto unique ─────

    public void UnlockAllUniqueCars()
    {
        for (int i = UniqueCarsStart; i < UniqueCarsEnd; i++) Data[i] = 1;
    }

    // ───── eventi ─────
    // Tabella da 0x72 byte: 2 = completato. Gli indici 0x54/0x55 (nel codice di avanzamento) e 0x55/0x56 (nel codice manuale)
    // sono trattati come coppia dall'originale, che e' incoerente su quale: non fidarti di questo dettaglio senza provare.

    public int GetEventByte(int index) => Data[EventTableOffset + CheckIndex(index)];
    public void SetEventByte(int index, byte value) => Data[EventTableOffset + CheckIndex(index)] = value;

    private static int CheckIndex(int i) => i is >= 0 and < EventTableLength ? i : throw new ArgumentOutOfRangeException(nameof(i));

    /// <summary>Conteggio come lo mostra l'originale: byte == 2 negli indici 0..0x70, meno 1 se > 85.</summary>
    public int PassedEventCount
    {
        get
        {
            int c = 0;
            for (int i = 0; i < EventEntries; i++) if (Data[EventTableOffset + i] == 2) c++;
            return c > 0x55 ? c - 1 : c;
        }
    }

    /// <summary>"Complete all events" dell'originale: tutti i 0x72 byte a 2.</summary>
    public void CompleteAllEvents()
    {
        for (int i = 0; i < EventTableLength; i++) Data[EventTableOffset + i] = 2;
    }

    /// <summary>"Advance / draw back to event N" dell'originale (N = 0..112), con le sue regole: avanza dal primo non completato, retrocede dall'ultimo.</summary>
    public void AdvanceOrDrawBackTo(int target)
    {
        if (target is < 0 or > 112) throw new ArgumentOutOfRangeException(nameof(target));
        int edi = target - PassedEventCount;
        int i = 0;
        if (edi != 0) Data[EventTableOffset + 0x71] = 0;
        while (edi > 0 && i < 0x71)
        {
            if (i == 0x54) Data[EventTableOffset + 0x55] = 2;
            if (Data[EventTableOffset + i] != 2) { Data[EventTableOffset + i] = 2; edi--; }
            i++;
        }
        i = 0x70;
        while (edi < 0 && i >= 0)
        {
            if (i == 0x55) Data[EventTableOffset + 0x54] = 1;
            if (Data[EventTableOffset + i] == 2) { Data[EventTableOffset + i] = 1; edi++; }
            i--;
        }
    }

    /// <summary>Indice nella tabella dell'evento n (0..112): se n > 85 e' n+1.</summary>
    public static int EventIndex(int n) =>
        n is >= 0 and <= 112 ? (n > 0x55 ? n + 1 : n) : throw new ArgumentOutOfRangeException(nameof(n));

    /// <summary>"Manual events passing" su una copia della tabella (0x72 byte): l'evento 85 tocca anche l'indice 86.</summary>
    public static void SetEventPassed(byte[] table, int n, bool passed)
    {
        int index = EventIndex(n);
        byte v = passed ? (byte)2 : (byte)0;
        table[index] = v;
        if (index == 0x55) table[0x56] = v;
    }

    public static bool IsEventPassed(byte[] table, int n) => table[EventIndex(n)] == 2;

    public void SetEventPassed(int n, bool passed)
    {
        var t = GetEventTableCopy();
        SetEventPassed(t, n, passed);
        SetEventTable(t);
    }

    public bool IsEventPassed(int n) => IsEventPassed(GetEventTableCopy(), n);

    public byte[] GetEventTableCopy() => Data.AsSpan(EventTableOffset, EventTableLength).ToArray();

    public void SetEventTable(byte[] table)
    {
        if (table.Length != EventTableLength) throw new ArgumentException("Tabella eventi di lunghezza errata.", nameof(table));
        table.CopyTo(Data, EventTableOffset);
    }

    // ───── applica un insieme di modifiche (stesso ordine dell'editor originale) ─────

    public static readonly Nfsu1Stat[] AllStats = Enum.GetValues<Nfsu1Stat>();
    public static readonly Nfsu1Upgrade[] AllUpgrades = Enum.GetValues<Nfsu1Upgrade>();

    /// <summary>Soldi, statistiche, auto unique, eventi, upgrade. Tutto viene validato prima di scrivere.</summary>
    public void Apply(Nfsu1Changes c)
    {
        if (c.Money is < 0) throw new ArgumentOutOfRangeException(nameof(c), "Soldi: atteso 0..2147483647.");
        if (c.Stats != null)
        {
            if (c.Stats.Length != AllStats.Length) throw new ArgumentException("17 statistiche attese.", nameof(c));
            if (c.Stats.Any(v => v < 0)) throw new ArgumentOutOfRangeException(nameof(c), "Statistiche: atteso 0..2147483647.");
        }
        if (c.UpgradeLevels != null && (c.UpgradeLevels.Length != AllUpgrades.Length || c.UpgradeLevels.Any(l => l is < 0 or > 3)))
            throw new ArgumentException("9 livelli upgrade attesi, ciascuno 0..3.", nameof(c));
        if (c.UpgradeUnique != null && c.UpgradeUnique.Length != AllUpgrades.Length)
            throw new ArgumentException("9 flag unique attesi.", nameof(c));
        if (c.Events == Nfsu1EventMode.AdvanceTo && c.EventTarget is < 0 or > 112) throw new ArgumentOutOfRangeException(nameof(c), "Evento: atteso 0..112.");
        if (c.Events == Nfsu1EventMode.Manual && (c.ManualTable == null || c.ManualTable.Length != EventTableLength))
            throw new ArgumentException("Tabella eventi manuale mancante o di lunghezza errata.", nameof(c));

        if (c.Money is { } m) Money = m;
        if (c.Stats != null) foreach (var st in AllStats) SetStat(st, c.Stats[(int)st]);
        if (c.UnlockUniqueCars) UnlockAllUniqueCars();
        switch (c.Events)
        {
            case Nfsu1EventMode.AdvanceTo: AdvanceOrDrawBackTo(c.EventTarget); break;
            case Nfsu1EventMode.Manual: SetEventTable(c.ManualTable!); break;
            case Nfsu1EventMode.CompleteAll: CompleteAllEvents(); break;
        }
        for (int i = 0; i < AllUpgrades.Length; i++)
        {
            if (c.UpgradeUnique != null) SetUpgradeUnique(AllUpgrades[i], c.UpgradeUnique[i]);
            if (c.UpgradeLevels != null) SetUpgradeLevel(AllUpgrades[i], c.UpgradeLevels[i]);
        }
    }

    // ───── salvataggio ─────

    /// <summary>Ricalcola l'hash e scrive il file. Come l'originale, prima copia il vecchio in "&lt;file&gt;.bak".</summary>
    public void Save(string path, bool backup = true)
    {
        RecalculateHash();
        if (backup && File.Exists(path)) File.Copy(path, path + ".bak", overwrite: true);
        File.WriteAllBytes(path, Data);
    }
}

public enum Nfsu1EventMode { Keep, AdvanceTo, Manual, CompleteAll }

/// <summary>Cosa cambiare nel salvataggio. I campi null/Keep lasciano il valore com'e'.</summary>
public sealed class Nfsu1Changes
{
    public int? Money { get; set; }
    /// <summary>17 valori, nell'ordine dell'enum Nfsu1Stat.</summary>
    public int[]? Stats { get; set; }
    public bool UnlockUniqueCars { get; set; }
    public Nfsu1EventMode Events { get; set; }
    public int EventTarget { get; set; }
    public byte[]? ManualTable { get; set; }
    /// <summary>9 valori, nell'ordine dell'enum Nfsu1Upgrade (1..9).</summary>
    public int[]? UpgradeLevels { get; set; }
    public bool[]? UpgradeUnique { get; set; }
}
