// Gestione dei profili di NFS Underground (cartella "NFS Underground": Profiles.usr, <NOME>.opt, <NOME>.cfg, <NOME>_<slot>.ugd).
// Il formato di questi file e' descritto da "nfsu1-web" di Luis Guilherme Lellis Vieira (MIT), letto da SPEED.EXE 1.4 e dai file
// che scrive il gioco: https://github.com/lguilherme44/nfsu1-web  (web/src/app/ug1save.ts).
//   Profiles.usr   u32 conteggio, poi 32 byte per profilo (nome + 0). Il gioco elenca SOLO i profili che sono qui.
//   <NOME>.opt     u32 0x13, u32 0, il nome (0-terminato, dall'offset 8), le opzioni
//   <NOME>.cfg     testo (opzioni video)
//   <NOME>_<slot>.ugd   il salvataggio: u32 0x13, il nome dello slot ("auto" per <NOME>_autosave), ...
using System.Buffers.Binary;
using System.Text;

namespace NFSU1ProfileMaster;

public sealed record ProfileInfo(string Name, bool InIndex, bool HasOpt, bool HasCfg, int Saves, string? Problem);

public enum IndexState { Missing, Valid, Invalid }

public sealed class ProfileStore
{
    public const uint Version = 0x13;
    public const string IndexFile = "Profiles.usr";
    public const int MaxNameLength = 15;
    private const int Slot = 32, OptNameOffset = 8;

    public string Folder { get; }
    /// <summary>Dove finiscono le copie di sicurezza prima di rinominare (Desktop\NFSU1_Backups).</summary>
    public string BackupRoot { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NFSU1_Backups");

    public ProfileStore(string folder) => Folder = folder;
    public string IndexPath => Path.Combine(Folder, IndexFile);

    // ───── Profiles.usr ─────

    private static uint U32(byte[] d, int o) => BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(o, 4));

    /// <summary>I nomi nell'indice, o null se non e' un indice valido (dimensione 4 + 32n, nomi non vuoti).</summary>
    public static List<string>? ParseIndex(byte[] d)
    {
        if (d.Length < 4) return null;
        uint n = U32(d, 0);
        if (n > 1000 || d.Length != 4 + (long)n * Slot) return null;
        var names = new List<string>();
        for (int i = 0; i < n; i++)
        {
            var sb = new StringBuilder();
            for (int k = 4 + i * Slot; k < 4 + (i + 1) * Slot && d[k] != 0; k++) sb.Append((char)d[k]);
            if (sb.Length == 0) return null;
            names.Add(sb.ToString());
        }
        return names;
    }

    /// <summary>Profiles.usr per questi nomi, con il resto di ogni slot a zero (come fa nfsu1-web).</summary>
    public static byte[] MakeIndex(IReadOnlyList<string> names)
    {
        var d = new byte[4 + names.Count * Slot];
        BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(0, 4), (uint)names.Count);
        for (int i = 0; i < names.Count; i++)
            for (int k = 0; k < names[i].Length && k < Slot - 1; k++) d[4 + i * Slot + k] = (byte)names[i][k];
        return d;
    }

    public IndexState ReadIndex(out List<string> names)
    {
        names = new List<string>();
        if (!File.Exists(IndexPath)) return IndexState.Missing;
        var parsed = ParseIndex(File.ReadAllBytes(IndexPath));
        if (parsed == null) return IndexState.Invalid;
        names = parsed;
        return IndexState.Valid;
    }

    // ───── .opt ─────

    /// <summary>Il nome dentro un .opt, o null se non e' uno di questa versione del gioco.</summary>
    public static string? OptName(byte[] d)
    {
        if (d.Length < 12 || U32(d, 0) != Version) return null;
        var sb = new StringBuilder();
        for (int k = OptNameOffset; k < OptNameOffset + Slot && k < d.Length && d[k] != 0; k++) sb.Append((char)d[k]);
        return sb.Length == 0 ? null : sb.ToString();
    }

    /// <summary>Copia del .opt con un altro nome. Il nome occupa la sua stringa e gli zeri che la seguono: mai altri dati.</summary>
    public static byte[] WithOptName(byte[] opt, string newName)
    {
        if (OptName(opt) == null) throw new InvalidDataException("opt");
        int end = Math.Min(opt.Length, OptNameOffset + Slot), i = OptNameOffset;
        while (i < end && opt[i] != 0) i++;
        while (i < end && opt[i] == 0) i++;
        int area = i - OptNameOffset;
        if (newName.Length > area - 1) throw new InvalidDataException("name too long for this .opt");
        var copy = (byte[])opt.Clone();
        for (int k = 0; k < area; k++) copy[OptNameOffset + k] = k < newName.Length ? (byte)newName[k] : (byte)0;
        return copy;
    }

    // ───── nomi ─────

    /// <summary>Un nome nuovo: 1-15 lettere o numeri (niente '_' perche' separa il nome dallo slot nei file .ugd).</summary>
    public static bool IsValidNewName(string n) =>
        n.Length is >= 1 and <= MaxNameLength && n.All(c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9'));

    // ───── scansione ─────

    private string[] FileNames() =>
        Directory.Exists(Folder) ? Directory.GetFiles(Folder).Select(p => Path.GetFileName(p)!).ToArray() : Array.Empty<string>();

    private static bool Is(string file, string ext) => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase);

    /// <summary>A quale profilo appartiene un .ugd: il nome noto piu' lungo che precede "_", altrimenti quello prima del primo "_".</summary>
    public static string? SaveOwner(string ugdFile, IEnumerable<string> knownNames)
    {
        string stem = Path.GetFileNameWithoutExtension(ugdFile);
        var known = knownNames.Where(n => stem.StartsWith(n + "_", StringComparison.OrdinalIgnoreCase)).OrderByDescending(n => n.Length).FirstOrDefault();
        if (known != null) return known;
        int u = stem.IndexOf('_');
        return u > 0 ? stem[..u] : null;
    }

    public List<ProfileInfo> Scan()
    {
        var files = FileNames();
        var optNames = files.Where(f => Is(f, ".opt")).Select(f => Path.GetFileNameWithoutExtension(f)!).ToList();
        ReadIndex(out var indexNames);
        var list = new List<ProfileInfo>();
        foreach (var name in optNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            string? problem = null;
            try
            {
                var inside = OptName(File.ReadAllBytes(Path.Combine(Folder, name + ".opt")));
                if (inside == null) problem = "opt";
                else if (!inside.Equals(name, StringComparison.OrdinalIgnoreCase)) problem = "optname";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { problem = "io"; }
            bool hasCfg = files.Any(f => f.Equals(name + ".cfg", StringComparison.OrdinalIgnoreCase));
            int saves = files.Count(f => Is(f, ".ugd") && SaveOwner(f, optNames)?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);
            bool inIndex = indexNames.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
            list.Add(new ProfileInfo(name, inIndex, true, hasCfg, saves, problem));
        }
        foreach (var n in indexNames)
            if (!optNames.Any(o => o.Equals(n, StringComparison.OrdinalIgnoreCase)))
                list.Add(new ProfileInfo(n, true, false, false, 0, "noopt"));
        return list;
    }

    // ───── operazioni ─────

    private static void WriteNew(string path, byte[] data)
    {
        using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write);     // non sovrascrive mai
        fs.Write(data, 0, data.Length);
    }

    private void BackupIndex()
    {
        if (File.Exists(IndexPath)) File.Copy(IndexPath, IndexPath + ".bak", overwrite: true);
    }

    /// <summary>Aggiunge i nomi a Profiles.usr. Se manca, lo crea con tutti i profili validi; se non ha il formato atteso, rifiuta.</summary>
    private void AddToIndex(IReadOnlyList<string> names)
    {
        var state = ReadIndex(out var current);
        if (state == IndexState.Invalid) throw new InvalidDataException("index");
        var toAdd = names.Where(n => !current.Any(c => c.Equals(n, StringComparison.OrdinalIgnoreCase))).ToList();
        if (toAdd.Count == 0 && state == IndexState.Valid) return;
        if (state == IndexState.Missing)
        {
            var all = Scan().Where(p => p.HasOpt && p.Problem == null).Select(p => p.Name)
                .Concat(toAdd).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            File.WriteAllBytes(IndexPath, MakeIndex(all));
            return;
        }
        BackupIndex();
        var data = File.ReadAllBytes(IndexPath);                      // i byte esistenti restano com'e' (anche il resto di ogni slot)
        var extra = MakeIndex(toAdd);
        var merged = new byte[data.Length + toAdd.Count * Slot];
        Buffer.BlockCopy(data, 0, merged, 0, data.Length);
        Buffer.BlockCopy(extra, 4, merged, data.Length, toAdd.Count * Slot);
        BinaryPrimitives.WriteUInt32LittleEndian(merged.AsSpan(0, 4), (uint)(current.Count + toAdd.Count));
        File.WriteAllBytes(IndexPath, merged);
    }

    /// <summary>Mette nell'elenco del gioco i profili che hanno i file ma non sono in Profiles.usr. Ritorna i nomi aggiunti.</summary>
    public List<string> RegisterMissing()
    {
        var missing = Scan().Where(p => p.HasOpt && p.Problem == null && !p.InIndex).Select(p => p.Name).ToList();
        if (missing.Count > 0) AddToIndex(missing);
        return missing;
    }

    private string? OptPath(string name) =>
        FileNames().Where(f => f.Equals(name + ".opt", StringComparison.OrdinalIgnoreCase)).Select(f => Path.Combine(Folder, f)).FirstOrDefault();

    /// <summary>I file di un profilo: .opt, .cfg e tutti i suoi .ugd (percorsi completi).</summary>
    public List<string> FilesOf(string name)
    {
        var files = FileNames();
        var opts = files.Where(f => Is(f, ".opt")).Select(f => Path.GetFileNameWithoutExtension(f)!).ToList();
        return files.Where(f =>
                f.Equals(name + ".opt", StringComparison.OrdinalIgnoreCase) || f.Equals(name + ".cfg", StringComparison.OrdinalIgnoreCase) ||
                (Is(f, ".ugd") && SaveOwner(f, opts)?.Equals(name, StringComparison.OrdinalIgnoreCase) == true))
            .Select(f => Path.Combine(Folder, f)).ToList();
    }

    private bool NameTaken(string name) =>
        FileNames().Any(f => Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase) ||
                             f.StartsWith(name + "_", StringComparison.OrdinalIgnoreCase)) ||
        (ReadIndex(out var n) == IndexState.Valid && n.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)));

    private static string Renamed(string file, string oldName, string newName) =>
        newName + Path.GetFileName(file).Substring(oldName.Length);          // <NOME>.opt / <NOME>.cfg / <NOME>_<slot>.ugd

    /// <summary>Crea un profilo nuovo come copia di un altro (stesse opzioni e stessi salvataggi) e lo mette nell'elenco del gioco.</summary>
    public List<string> Clone(string source, string newName)
    {
        if (!IsValidNewName(newName)) throw new ArgumentException("name", nameof(newName));
        if (NameTaken(newName)) throw new IOException("exists");
        string optPath = OptPath(source) ?? throw new FileNotFoundException("opt");
        var newOpt = WithOptName(File.ReadAllBytes(optPath), newName);       // puo' rifiutare (nome troppo lungo per quel .opt)
        if (ReadIndex(out _) == IndexState.Invalid) throw new InvalidDataException("index");

        var created = new List<string>();
        try
        {
            foreach (var f in FilesOf(source))
            {
                string target = Path.Combine(Folder, Renamed(f, source, newName));
                WriteNew(target, Is(f, ".opt") ? newOpt : File.ReadAllBytes(f));
                created.Add(target);
            }
            AddToIndex(new[] { newName });
        }
        catch
        {
            foreach (var c in created) { try { File.Delete(c); } catch { /* rollback: meglio un file in piu' che un errore in piu' */ } }
            throw;
        }
        return created;
    }

    /// <summary>
    /// Rinomina un profilo: nuovi file con il nuovo nome, indice aggiornato nella stessa posizione, poi via i vecchi.
    /// Prima copia tutto in BackupRoot. Ritorna la cartella della copia.
    /// </summary>
    public string Rename(string oldName, string newName)
    {
        if (!IsValidNewName(newName)) throw new ArgumentException("name", nameof(newName));
        if (oldName.Equals(newName, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("same", nameof(newName));
        if (NameTaken(newName)) throw new IOException("exists");
        string optPath = OptPath(oldName) ?? throw new FileNotFoundException("opt");
        var newOpt = WithOptName(File.ReadAllBytes(optPath), newName);
        if (ReadIndex(out _) == IndexState.Invalid) throw new InvalidDataException("index");

        // copia di sicurezza di tutto cio' che si tocca
        string backup = Path.Combine(BackupRoot, $"{DateTime.Now:yyyyMMdd_HHmmss}_{oldName}");
        Directory.CreateDirectory(backup);
        var oldFiles = FilesOf(oldName);
        foreach (var f in oldFiles) File.Copy(f, Path.Combine(backup, Path.GetFileName(f)), overwrite: true);
        if (File.Exists(IndexPath)) File.Copy(IndexPath, Path.Combine(backup, IndexFile), overwrite: true);

        var created = new List<string>();
        try
        {
            foreach (var f in oldFiles)
            {
                string target = Path.Combine(Folder, Renamed(f, oldName, newName));
                WriteNew(target, Is(f, ".opt") ? newOpt : File.ReadAllBytes(f));
                created.Add(target);
            }
            ReplaceInIndex(oldName, newName);
        }
        catch
        {
            foreach (var c in created) { try { File.Delete(c); } catch { /* vedi sopra */ } }
            if (File.Exists(Path.Combine(backup, IndexFile))) File.Copy(Path.Combine(backup, IndexFile), IndexPath, overwrite: true);
            throw;
        }
        foreach (var f in oldFiles)
        {
            try { File.Delete(f); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* resta, ma il nuovo e' a posto */ }
        }
        return backup;
    }

    private void ReplaceInIndex(string oldName, string newName)
    {
        var state = ReadIndex(out var names);
        int at = names.FindIndex(n => n.Equals(oldName, StringComparison.OrdinalIgnoreCase));
        if (state != IndexState.Valid || at < 0) { AddToIndex(new[] { newName }); return; }
        BackupIndex();
        var d = File.ReadAllBytes(IndexPath);
        for (int k = 0; k < Slot; k++) d[4 + at * Slot + k] = k < newName.Length ? (byte)newName[k] : (byte)0;
        File.WriteAllBytes(IndexPath, d);
    }
}
