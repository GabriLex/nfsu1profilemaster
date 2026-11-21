using System.Globalization;
using Nfsu1;

namespace NFSU1ProfileMaster;

internal sealed class BufferedPanel : Panel
{
    public BufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}

public sealed class MainForm : Form
{
    private const int W = 540, HeaderH = 100, PanelW = 512, PanelH = 372, PanelTop = 188, TabCount = 5, TabW = 98;
    private static readonly Color White = Color.White;     // tutti i testi del menu
    private static readonly string[] TabKeys = { "tab_save", "tab_unlock", "tab_car", "tab_custom", "tab_profiles" };

    private static readonly (string Key, Nfsu1Upgrade Upgrade)[] CarRows =
    {
        ("car_engine", Nfsu1Upgrade.Engine), ("car_transmission", Nfsu1Upgrade.Transmission), ("car_tires", Nfsu1Upgrade.Tires),
        ("car_ecu", Nfsu1Upgrade.Ecu), ("car_turbo", Nfsu1Upgrade.Turbo), ("car_brakes", Nfsu1Upgrade.Brakes),
        ("car_weight", Nfsu1Upgrade.WeightReduction), ("car_suspension", Nfsu1Upgrade.Suspension), ("car_nos", Nfsu1Upgrade.Nos),
    };

    private readonly List<(Control Ctl, string Key, bool Upper)> _i18n = new();
    private readonly Panel[] _tabs = new Panel[TabCount];
    private readonly Button[] _tabButtons = new Button[TabCount];
    private readonly RadioButton[][] _levels = new RadioButton[9][];
    private readonly CheckBox[] _uniques = new CheckBox[9];
    private readonly TextBox[] _stats = new TextBox[17];
    private readonly List<Label> _nameHints = new();

    private Nfsu1Save? _save;
    private ProfileStore? _store;
    private List<ProfileInfo> _profiles = new();
    private TextBox _prFolder = null!;
    private ListBox _prList = null!;
    private string? _path;
    private string _origExt = ".ugd";
    private byte[] _manualTable = new byte[Nfsu1Save.EventTableLength];
    private bool _loading, _searching;
    private CancellationTokenSource? _searchCts;

    private TextBox _file = null!, _name = null!, _fileName = null!, _date = null!, _bank = null!, _change = null!;
    private Label _fileExt = null!, _nameHint = null!;
    private Panel _lamp = null!;
    private CheckBox _uniqueCars = null!, _passed = null!;
    private RadioButton _radNone = null!, _radAdvance = null!, _radManual = null!, _radComplete = null!;
    private NumericUpDown _eventsNum = null!, _manualNum = null!;
    private Label _status = null!;

    public MainForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Bg;
        AutoScroll = true;
        AllowDrop = true;
        DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) LoadFile(files[0]);
        };
        FormClosing += (_, _) => _searchCts?.Cancel();
        Shown += (_, _) => _ = RunSearchAsync(auto: true);       // ricerca automatica all'avvio

        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon; } catch { /* icona opzionale */ }

        BuildUi();
        ApplyLanguage();
        SelectTab(0);
        UpdateEnabled();
        SetProfileFolder(GuessProfileFolder());
    }

    // ───────────────────────── interfaccia ─────────────────────────

    private void BuildUi()
    {
        var header = new BufferedPanel { Left = 0, Top = 0, Width = W, Height = HeaderH, BackColor = Theme.Bg };
        header.Paint += PaintHeader;
        Controls.Add(header);
        int lx = W - 82;
        foreach (var code in new[] { "IT", "EN" })
        {
            var b = Theme.MakeButton(code, White, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 9);
            b.SetBounds(lx, 12, 34, 24);
            b.Click += (_, _) => { Strings.Current = code; ApplyLanguage(); };
            header.Controls.Add(b);
            lx += 36;
        }

        // riga del file: percorso, lampadina, CERCA, FILE
        _file = new TextBox { ReadOnly = true };
        StyleBox(_file, 14, 108, 276, White);
        Controls.Add(_file);
        _lamp = new Panel { Left = 298, Top = 115, Width = 14, Height = 14, BackColor = Theme.Bg };
        _lamp.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var br = new SolidBrush(_save != null ? Theme.SelGreen : Theme.Border);
            e.Graphics.FillEllipse(br, 0, 0, 12, 12);
        };
        Controls.Add(_lamp);
        var searchBtn = Theme.MakeButton("", White, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 12);
        searchBtn.SetBounds(322, 106, 100, 32);
        searchBtn.Click += (_, _) => _ = RunSearchAsync(auto: false);
        _i18n.Add((searchBtn, "btn_search", true));
        Controls.Add(searchBtn);
        var fileBtn = Theme.MakeButton("", White, Theme.SelDark, Theme.SelRing, Theme.SelDarker, Theme.SelGreen, 12);
        fileBtn.SetBounds(430, 106, 96, 32);
        fileBtn.Click += (_, _) => OpenFileDialog();
        _i18n.Add((fileBtn, "file_btn", true));
        Controls.Add(fileBtn);

        // schede
        for (int i = 0; i < TabCount; i++)
        {
            int idx = i;
            var b = Theme.MakeButton("", White, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 10);
            b.SetBounds(14 + i * (TabW + 5), 148, TabW, 34);
            b.Click += (_, _) => SelectTab(idx);
            _i18n.Add((b, TabKeys[i], true));
            _tabButtons[i] = b;
            Controls.Add(b);

            var p = new Panel { Left = 14, Top = PanelTop, Width = PanelW, Height = PanelH, BackColor = Theme.PanelBg };
            p.Paint += (_, e) => { using var pen = new Pen(Theme.Border); e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1); };
            _tabs[i] = p;
            Controls.Add(p);
        }
        BuildProfile(_tabs[0]);
        BuildUnlock(_tabs[1]);
        BuildCar(_tabs[2]);
        BuildCustom(_tabs[3]);
        BuildProfiles(_tabs[4]);

        // pulsanti in basso
        int y = PanelTop + PanelH + 12;
        var patch = Theme.MakeButton("", White, Theme.SelDark, Theme.SelRing, Theme.SelDarker, Theme.SelGreen, 14);
        patch.SetBounds(14, y, 262, 46);
        patch.Click += (_, _) => Patch();
        _i18n.Add((patch, "btn_patch", true));
        Controls.Add(patch);
        var about = Theme.MakeButton("", White, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 12);
        about.SetBounds(284, y, 118, 46);
        about.Click += (_, _) => ThemedDialog.Show(this, Strings.Get("t_about"), string.Format(Strings.Get("about_text"), AppVersion.Text), DialogKind.Info);
        _i18n.Add((about, "btn_about", true));
        Controls.Add(about);
        var exit = Theme.MakeButton("", White, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 12);
        exit.SetBounds(410, y, 116, 46);
        exit.Click += (_, _) => Close();
        _i18n.Add((exit, "btn_exit", true));
        Controls.Add(exit);

        _status = new Label
        {
            Left = 14, Top = y + 54, Width = 512, Height = 22, AutoSize = false, ForeColor = White,
            Font = Theme.Mono(9), BackColor = Theme.Bg,
        };
        Controls.Add(_status);

        var size = new Size(W, y + 54 + 22 + 10);
        int maxH = Screen.PrimaryScreen is { } scr ? scr.WorkingArea.Height - 80 : size.Height;
        bool scroll = size.Height > maxH;
        ClientSize = new Size(size.Width + (scroll ? SystemInformation.VerticalScrollBarWidth : 0), Math.Min(size.Height, maxH));
    }

    private void BuildProfile(Panel p)
    {
        // nome del profilo (dentro il salvataggio)
        Lbl(p, "lbl_profile_name", 12, 16, 154);
        _name = new TextBox(); StyleBox(_name, 170, 12, 200, White); p.Controls.Add(_name);
        _name.TextChanged += (_, _) => Mark(_name, false);
        _nameHint = Hint(p, "hint_profile_name", 170, 42, 330);

        // nome del file sul disco
        Lbl(p, "lbl_file_name", 12, 66, 154);
        _fileName = new TextBox { MaxLength = 80 }; StyleBox(_fileName, 170, 62, 250, White); p.Controls.Add(_fileName);
        _fileName.TextChanged += (_, _) => Mark(_fileName, false);
        _fileExt = new Label
        {
            Text = ".ugd", Left = 424, Top = 66, Width = 76, Height = 22, AutoSize = false, ForeColor = White,
            Font = Theme.Impact(11), TextAlign = ContentAlignment.MiddleLeft, BackColor = Theme.PanelBg,
        };
        p.Controls.Add(_fileExt);
        Hint(p, "hint_file_name", 170, 92, 330);

        // data
        Lbl(p, "lbl_date", 12, 116, 154);
        _date = new TextBox(); StyleBox(_date, 170, 112, 250, White); p.Controls.Add(_date);
        _date.TextChanged += (_, _) => Mark(_date, false);
        var now = Theme.MakeButton("", White, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 10);
        now.SetBounds(428, 111, 72, 30);
        now.Click += (_, _) => { if (_save != null) _date.Text = DateTime.Now.ToString("h:mm tt M/d/yyyy", CultureInfo.InvariantCulture); };
        _i18n.Add((now, "btn_now", true));
        p.Controls.Add(now);
        Hint(p, "hint_date", 170, 142, 330);

        p.Controls.Add(new Panel { Left = 12, Top = 170, Width = 488, Height = 1, BackColor = Theme.LineSep });

        // soldi
        Lbl(p, "lbl_bank", 12, 186, 154);
        _bank = new TextBox { ReadOnly = true }; StyleBox(_bank, 170, 182, 200, White); p.Controls.Add(_bank);
        Lbl(p, "lbl_newmoney", 12, 224, 154);
        _change = new TextBox { MaxLength = 10 }; StyleBox(_change, 170, 220, 200, White); p.Controls.Add(_change);
        _change.TextChanged += (_, _) => Mark(_change, false);
        int qx = 170;
        foreach (var amount in new[] { 100_000, 1_000_000, 9_999_999 })
        {
            int value = amount;
            var q = Theme.MakeButton(amount.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.'), White, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 10);
            q.SetBounds(qx, 256, 100, 30);
            q.Click += (_, _) => { if (_save != null) _change.Text = value.ToString(CultureInfo.InvariantCulture); };
            p.Controls.Add(q);
            qx += 108;
        }
        Hint(p, "hint_money", 12, 298, 488, 62);
    }

    private void BuildUnlock(Panel p)
    {
        _uniqueCars = Check(p, "chk_unique_cars", 12, 14, 460);
        p.Controls.Add(new Panel { Left = 12, Top = 48, Width = 488, Height = 1, BackColor = Theme.LineSep });

        _radNone = Radio(p, "rad_none", 12, 58, 460);
        _radAdvance = Radio(p, "rad_advance", 12, 90, 300);
        _eventsNum = Num(p, 318, 88);
        Lbl(p, "lbl_of", 392, 93, 90);
        _radManual = Radio(p, "rad_manual", 12, 126, 300);
        _manualNum = Num(p, 318, 124);
        _passed = Check(p, "chk_passed", 392, 127, 110);
        _radComplete = Radio(p, "rad_complete", 12, 162, 460);
        _radNone.Checked = true;
        foreach (var r in new[] { _radNone, _radAdvance, _radManual, _radComplete }) r.CheckedChanged += (_, _) => UpdateEnabled();

        _manualNum.ValueChanged += (_, _) => RefreshPassedBox();
        _passed.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            Nfsu1Save.SetEventPassed(_manualTable, (int)_manualNum.Value, _passed.Checked);
        };

        p.Controls.Add(new Panel { Left = 12, Top = 198, Width = 488, Height = 1, BackColor = Theme.LineSep });
        var guide = Theme.MakeButton("", White, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 11);
        guide.SetBounds(12, 210, 488, 34);
        guide.Click += (_, _) => ThemedDialog.Show(this, Strings.Get("t_guide"), Strings.Get("guide_text"), DialogKind.Info);
        _i18n.Add((guide, "btn_guide", true));
        p.Controls.Add(guide);
        var hint = new Label
        {
            Left = 12, Top = 252, Width = 488, Height = 36, AutoSize = false, ForeColor = White, Font = Theme.Mono(8),
            BackColor = Theme.PanelBg,
        };
        _i18n.Add((hint, "hint_game", false));
        p.Controls.Add(hint);
    }

    private void BuildCar(Panel p)
    {
        for (int k = 0; k < 4; k++)
            p.Controls.Add(new Label
            {
                Text = k == 0 ? "S" : k.ToString(), Left = 172 + k * 56, Top = 8, Width = 52, Height = 20, AutoSize = false,
                ForeColor = White, Font = Theme.Mono(9), TextAlign = ContentAlignment.MiddleLeft, BackColor = Theme.PanelBg,
            });
        for (int i = 0; i < CarRows.Length; i++)
        {
            int y = 34 + i * 34;
            Lbl(p, CarRows[i].Key, 12, y + 3, 150);
            var row = new Panel { Left = 172, Top = y, Width = 224, Height = 28, BackColor = Theme.PanelBg };
            _levels[i] = new RadioButton[4];
            for (int k = 0; k < 4; k++)
            {
                var r = new WhiteRadioButton
                {
                    Text = k == 0 ? "S" : k.ToString(), Left = k * 56, Top = 2, Width = 52, Height = 24, AutoSize = false,
                    ForeColor = White, BackColor = Theme.PanelBg, Font = Theme.Impact(10.5f), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                };
                _levels[i][k] = r;
                row.Controls.Add(r);
            }
            p.Controls.Add(row);
            _uniques[i] = Check(p, "car_unique", 404, y + 2, 100);
        }
    }

    private void BuildCustom(Panel p)
    {
        string[] modes = { "Circuit", "Sprint", "Drift", "Drag" };
        for (int c = 0; c < 4; c++)
            p.Controls.Add(new Label
            {
                Text = modes[c].ToUpperInvariant(), Left = 112 + c * 100, Top = 8, Width = 92, Height = 22, AutoSize = false,
                ForeColor = White, Font = Theme.Impact(11), TextAlign = ContentAlignment.MiddleCenter, BackColor = Theme.PanelBg,
            });
        string[] rowKeys = { "row_starts", "row_wins", "row_style", "row_totaled" };
        for (int r = 0; r < 4; r++) Lbl(p, rowKeys[r], 12, 40 + r * 36, 96);

        var grid = new (Nfsu1Stat Stat, int Col, int Row)[]
        {
            (Nfsu1Stat.CircuitStarts, 0, 0), (Nfsu1Stat.CircuitWins, 0, 1), (Nfsu1Stat.CircuitStylePoints, 0, 2),
            (Nfsu1Stat.SprintStarts, 1, 0), (Nfsu1Stat.SprintWins, 1, 1), (Nfsu1Stat.SprintStylePoints, 1, 2),
            (Nfsu1Stat.DriftStarts, 2, 0), (Nfsu1Stat.DriftWins, 2, 1), (Nfsu1Stat.DriftStylePoints, 2, 2),
            (Nfsu1Stat.DragStarts, 3, 0), (Nfsu1Stat.DragWins, 3, 1), (Nfsu1Stat.DragStylePoints, 3, 2), (Nfsu1Stat.DragTimesTotaled, 3, 3),
        };
        foreach (var (stat, col, row) in grid) _stats[(int)stat] = StatBox(p, 112 + col * 100, 36 + row * 36, 92);

        p.Controls.Add(new Panel { Left = 12, Top = 196, Width = 488, Height = 1, BackColor = Theme.LineSep });
        Lbl(p, "tot_starts", 12, 216, 150); _stats[(int)Nfsu1Stat.TotalStarts] = StatBox(p, 170, 212, 110);
        Lbl(p, "tot_wins", 12, 254, 150); _stats[(int)Nfsu1Stat.TotalWins] = StatBox(p, 170, 250, 110);
        Lbl(p, "tot_losses", 12, 292, 150); _stats[(int)Nfsu1Stat.TotalLosses] = StatBox(p, 170, 288, 110);
        Lbl(p, "tot_style", 292, 292, 130); _stats[(int)Nfsu1Stat.TotalStylePoints] = StatBox(p, 418, 288, 86);
    }

    private TextBox StatBox(Panel p, int x, int y, int w)
    {
        var t = new TextBox { MaxLength = 10 };
        StyleBox(t, x, y, w, White);
        t.TextChanged += (_, _) => Mark(t, false);
        p.Controls.Add(t);
        return t;
    }

    private void PaintHeader(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using (var bg = new SolidBrush(Theme.PanelBg)) g.FillRectangle(bg, 15, 9, W - 30, HeaderH - 16);
        using (var pb = new Pen(Theme.Border)) g.DrawRectangle(pb, 14, 8, W - 28, HeaderH - 14);
        using (var pl = new Pen(Theme.BorderLt)) { g.DrawLine(pl, 14, 8, W - 14, 8); g.DrawLine(pl, 14, 8, 14, HeaderH - 6); }
        DrawCentered(g, "NEED FOR SPEED", Theme.Impact(13), White, 28);
        DrawCentered(g, "UNDERGROUND", Theme.Impact(30), White, 52);
        using (var b1 = new SolidBrush(Theme.SelGreen)) g.FillRectangle(b1, 60, 68, W - 120, 2);
        using (var b2 = new SolidBrush(Theme.SelRing)) g.FillRectangle(b2, 60, 71, W - 120, 1);
        DrawCentered(g, "PROFILES  MASTER", Theme.Impact(12), White, 84);
    }

    private static void DrawCentered(Graphics g, string text, Font font, Color color, int cy) =>
        TextRenderer.DrawText(g, text, font, new Rectangle(0, cy - 30, W, 60), color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

    // ───────────────────────── piccoli costruttori ─────────────────────────

    private Label Lbl(Control parent, string key, int x, int y, int w)
    {
        var l = new Label
        {
            Left = x, Top = y, Width = w, Height = 22, AutoSize = false, ForeColor = White, Font = Theme.Impact(11),
            TextAlign = ContentAlignment.MiddleLeft, BackColor = Theme.PanelBg,
        };
        _i18n.Add((l, key, false));
        parent.Controls.Add(l);
        return l;
    }

    private Label Hint(Control parent, string key, int x, int y, int w, int h = 18)
    {
        var l = new Label
        {
            Left = x, Top = y, Width = w, Height = h, AutoSize = false, ForeColor = White, Font = Theme.Mono(8), BackColor = Theme.PanelBg,
        };
        _i18n.Add((l, key, false));
        parent.Controls.Add(l);
        return l;
    }

    private CheckBox Check(Control parent, string key, int x, int y, int w)
    {
        var c = new WhiteCheckBox
        {
            Left = x, Top = y, Width = w, Height = 24, AutoSize = false, ForeColor = White, BackColor = Theme.PanelBg,
            Font = Theme.Impact(10.5f), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
        };
        _i18n.Add((c, key, false));
        parent.Controls.Add(c);
        return c;
    }

    private RadioButton Radio(Control parent, string key, int x, int y, int w)
    {
        var r = new WhiteRadioButton
        {
            Left = x, Top = y, Width = w, Height = 26, AutoSize = false, ForeColor = White, BackColor = Theme.PanelBg,
            Font = Theme.Impact(10.5f), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
        };
        _i18n.Add((r, key, false));
        parent.Controls.Add(r);
        return r;
    }

    private NumericUpDown Num(Control parent, int x, int y)
    {
        var n = new NumericUpDown
        {
            Left = x, Top = y, Width = 64, Minimum = 0, Maximum = 112, BackColor = Theme.PanelBg, ForeColor = White,
            Font = Theme.Impact(11), BorderStyle = BorderStyle.FixedSingle,
        };
        parent.Controls.Add(n);
        return n;
    }

    private static void StyleBox(TextBox t, int x, int y, int w, Color fore)
    {
        t.SetBounds(x, y, w, 28);
        t.BackColor = Theme.PanelBg; t.ForeColor = fore; t.Font = Theme.Impact(11); t.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void Mark(TextBox t, bool bad) => t.BackColor = bad ? Color.FromArgb(90, 25, 35) : Theme.PanelBg;

    // ───────────────────────── lingua / schede / stato ─────────────────────────

    private void ApplyLanguage()
    {
        Text = Strings.Get("title");
        int nameMax = _save?.NameMaxLength ?? 15;
        foreach (var (ctl, key, upper) in _i18n)
        {
            string t = Strings.Get(key);
            if (key == "hint_profile_name") t = string.Format(t, nameMax);
            ctl.Text = upper ? t.ToUpperInvariant() : t;
        }
        if (_path == null) _file.Text = Strings.Get("file_none");
        FillProfileList();
    }

    private void SelectTab(int index)
    {
        if (index == 4 && _store != null) RefreshProfiles();
        for (int i = 0; i < TabCount; i++)
        {
            bool active = i == index;
            _tabs[i].Visible = active;
            _tabButtons[i].ForeColor = White;
            _tabButtons[i].BackColor = active ? Theme.SelDark : Theme.BtnNorm;
            _tabButtons[i].FlatAppearance.BorderColor = active ? Theme.SelRing : Theme.Border;
        }
    }

    private void UpdateEnabled()
    {
        bool loaded = _save != null;
        // Campi di testo in sola lettura (il testo resta bianco); i controlli di scelta usano il disegno bianco anche da disabilitati.
        // I contatori restano sempre attivi: contano solo se la relativa opzione e' selezionata.
        _name.ReadOnly = !loaded; _fileName.ReadOnly = !loaded; _date.ReadOnly = !loaded; _change.ReadOnly = !loaded;
        foreach (var t in _stats) t.ReadOnly = !loaded;
        _uniqueCars.Enabled = loaded;
        foreach (var r in new[] { _radNone, _radAdvance, _radManual, _radComplete }) r.Enabled = loaded;
        _passed.Enabled = loaded && _radManual.Checked;
        foreach (var row in _levels) foreach (var r in row) r.Enabled = loaded;
        foreach (var u in _uniques) u.Enabled = loaded;
        _lamp.Invalidate();
    }

    private void RefreshPassedBox()
    {
        _loading = true;
        try { _passed.Checked = Nfsu1Save.IsEventPassed(_manualTable, (int)_manualNum.Value); }
        finally { _loading = false; }
    }

    // ───────────────────────── ricerca e caricamento ─────────────────────────

    private async Task RunSearchAsync(bool auto)
    {
        if (_searching) return;
        _searching = true;
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        _status.Text = Strings.Get("search_running");

        List<FoundSave> found;
        try { found = await Task.Run(() => SaveFinder.Find(SaveFinder.DefaultRoots(), cts.Token), cts.Token); }
        catch (OperationCanceledException) { _searching = false; return; }
        catch (Exception) { found = new List<FoundSave>(); }      // problema imprevisto: nessun risultato
        _searching = false;
        if (IsDisposed || Disposing) return;

        if (found.Count == 0)
        {
            _status.Text = Strings.Get("search_none");
            return;
        }
        if (auto)
        {
            if (_path == null)
            {
                LoadFile(found[0].Path);                           // il piu' recente
                _status.Text = string.Format(Strings.Get("search_found_auto"), found.Count);
            }
            else _status.Text = string.Format(Strings.Get("search_found"), found.Count);
            return;
        }

        int pick = 0;
        if (found.Count > 1)
        {
            var labels = found.Select(f =>
                $"{(f.Name.Length > 0 ? f.Name : "?")}  ·  {f.Modified:g}  ·  {f.Path}").ToList();
            pick = PickDialog.Choose(this, Strings.Get("search_pick_title"), Strings.Get("search_pick_msg"), labels);
        }
        if (pick >= 0) LoadFile(found[pick].Path);
        _status.Text = string.Format(Strings.Get("search_found"), found.Count);
    }

    private void OpenFileDialog()
    {
        using var dlg = new OpenFileDialog
        {
            Title = Strings.Get("dlg_open"), Filter = "NFS7 savegame (*.ugd)|*.ugd|*.*|*.*", CheckFileExists = true,
        };
        if (_path != null) dlg.InitialDirectory = Path.GetDirectoryName(_path);
        if (dlg.ShowDialog(this) == DialogResult.OK) LoadFile(dlg.FileName);
    }

    private void Reset()
    {
        _save = null; _path = null;
        _file.Text = Strings.Get("file_none");
        UpdateEnabled();
    }

    private void LoadFile(string path)
    {
        try
        {
            var s = Nfsu1Save.Load(path);
            _save = s; _path = path;
            Populate(s);
            _status.Text = Strings.Get("status_loaded");
            if (_store == null) SetProfileFolder(GuessProfileFolder());
        }
        catch (InvalidDataException)
        {
            Reset();
            ThemedDialog.Show(this, Strings.Get("t_error"), Strings.Get("msg_invalid"), DialogKind.Error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Reset();
            ThemedDialog.Show(this, Strings.Get("t_error"), Strings.Get("msg_io"), DialogKind.Error);
        }
    }

    private void Populate(Nfsu1Save s)
    {
        _loading = true;
        try
        {
            _file.Text = _path ?? "";
            _name.MaxLength = Math.Max(1, s.NameMaxLength); _name.Text = s.Name;
            _date.MaxLength = Math.Max(1, s.DateMaxLength); _date.Text = s.Date;
            _origExt = Path.GetExtension(_path) ?? "";
            _fileExt.Text = _origExt;
            _fileName.Text = Path.GetFileNameWithoutExtension(_path) ?? "";
            _bank.Text = s.Money.ToString(CultureInfo.InvariantCulture);
            _change.Text = "";
            foreach (var t in new[] { _name, _date, _fileName, _change }) Mark(t, false);
            _uniqueCars.Checked = false;
            _radNone.Checked = true;
            _eventsNum.Value = Math.Min(112, s.PassedEventCount);
            _manualTable = s.GetEventTableCopy();
            _manualNum.Value = 0;
            _passed.Checked = Nfsu1Save.IsEventPassed(_manualTable, 0);
            foreach (var st in Nfsu1Save.AllStats)
            {
                _stats[(int)st].Text = s.GetStat(st).ToString(CultureInfo.InvariantCulture);
                Mark(_stats[(int)st], false);
            }
            for (int i = 0; i < CarRows.Length; i++)
            {
                int level = s.GetUpgradeLevel(CarRows[i].Upgrade);
                for (int k = 0; k < 4; k++) _levels[i][k].Checked = k == level;     // fuori 0..3: nessuna scelta, come l'originale
                _uniques[i].Checked = s.GetUpgradeUnique(CarRows[i].Upgrade);
            }
        }
        finally { _loading = false; }
        UpdateEnabled();
        ApplyLanguage();      // aggiorna il limite del nome nel suggerimento
    }

    // ───────────────────────── profili (Profiles.usr) ─────────────────────────

    private void BuildProfiles(Panel p)
    {
        _prFolder = new TextBox { ReadOnly = true };
        StyleBox(_prFolder, 12, 12, 374, White);
        p.Controls.Add(_prFolder);
        var folderBtn = Theme.MakeButton("", White, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 10);
        folderBtn.SetBounds(394, 10, 106, 32);
        folderBtn.Click += (_, _) => ChooseProfileFolder();
        _i18n.Add((folderBtn, "pr_folder_btn", true));
        p.Controls.Add(folderBtn);

        _prList = new ListBox
        {
            Left = 12, Top = 50, Width = 488, Height = 146, BackColor = Theme.PanelBg, ForeColor = White, Font = Theme.Impact(10.5f),
            BorderStyle = BorderStyle.FixedSingle, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 24,
        };
        _prList.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(sel ? Theme.SelDark : Theme.PanelBg)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, _prList.Items[e.Index]?.ToString() ?? "", e.Font, Rectangle.Inflate(e.Bounds, -6, 0), White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        };
        p.Controls.Add(_prList);

        Hint(p, "pr_hint", 12, 202, 488, 40);
        ProfileButton(p, "pr_register", 12, 246, RegisterProfiles);
        ProfileButton(p, "pr_refresh", 260, 246, RefreshProfiles);
        ProfileButton(p, "pr_new", 12, 286, NewProfile);
        ProfileButton(p, "pr_rename", 260, 286, RenameProfile);
        Hint(p, "pr_warn", 12, 328, 488, 36);
    }

    private void ProfileButton(Control parent, string key, int x, int y, Action onClick)
    {
        var b = Theme.MakeButton("", White, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 11);
        b.SetBounds(x, y, 240, 34);
        b.Click += (_, _) => onClick();
        _i18n.Add((b, key, true));
        parent.Controls.Add(b);
    }

    /// <summary>La cartella dei profili piu' probabile: quella del salvataggio aperto, poi le cartelle tipiche del gioco.</summary>
    private string? GuessProfileFolder()
    {
        static bool Looks(string d)
        {
            try { return Directory.Exists(d) && (File.Exists(Path.Combine(d, ProfileStore.IndexFile)) || Directory.EnumerateFiles(d, "*.opt").Any()); }
            catch { return false; }
        }
        var candidates = new List<string>();
        if (_path != null) candidates.Add(Path.GetDirectoryName(_path) ?? "");
        candidates.AddRange(SaveFinder.DefaultRoots().Where(r => r.Depth <= 2).Select(r => r.Root));
        foreach (var c in candidates) if (c.Length > 0 && Looks(c)) return c;
        return null;
    }

    private void SetProfileFolder(string? folder)
    {
        _store = string.IsNullOrEmpty(folder) ? null : new ProfileStore(folder);
        _prFolder.Text = folder ?? "";
        RefreshProfiles();
    }

    private void ChooseProfileFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = Strings.Get("pr_pick_folder"), ShowNewFolderButton = false };
        if (_store != null && Directory.Exists(_store.Folder)) dlg.SelectedPath = _store.Folder;
        if (dlg.ShowDialog(this) == DialogResult.OK) SetProfileFolder(dlg.SelectedPath);
    }

    private void RefreshProfiles()
    {
        try { _profiles = _store?.Scan() ?? new List<ProfileInfo>(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _profiles = new List<ProfileInfo>();
            ShowError("msg_io");
        }
        FillProfileList();
    }

    private string ProfileLine(ProfileInfo pi)
    {
        string status = pi.Problem switch
        {
            "noopt" => Strings.Get("pr_st_noopt"),
            "opt" or "optname" or "io" => Strings.Get("pr_st_badopt"),
            _ => pi.InIndex ? Strings.Get("pr_st_ok") : Strings.Get("pr_st_hidden"),
        };
        string extra = pi.HasOpt
            ? "  ·  " + string.Format(Strings.Get("pr_saves"), pi.Saves) + (pi.HasCfg ? "" : "  ·  " + Strings.Get("pr_nocfg"))
            : "";
        return $"{pi.Name}  ·  {status}{extra}";
    }

    private ProfileInfo? SelectedProfile() =>
        _prList.SelectedIndex >= 0 && _prList.SelectedIndex < _profiles.Count ? _profiles[_prList.SelectedIndex] : null;

    private void FillProfileList(string? select = null)
    {
        string? keep = select ?? SelectedProfile()?.Name;
        _prList.Items.Clear();
        if (_profiles.Count == 0) { _prList.Items.Add(Strings.Get("pr_none")); return; }
        foreach (var pi in _profiles) _prList.Items.Add(ProfileLine(pi));
        int idx = keep == null ? 0 : _profiles.FindIndex(x => x.Name.Equals(keep, StringComparison.OrdinalIgnoreCase));
        _prList.SelectedIndex = Math.Max(0, idx);
    }

    private void ShowProfileError(Exception ex)
    {
        string key = ex switch
        {
            InvalidDataException { Message: "index" } => "pr_err_index",
            InvalidDataException => "pr_err_opt",
            ArgumentException => "pr_err_name",
            FileNotFoundException => "pr_err_noopt",
            IOException { Message: "exists" } => "pr_err_exists",
            _ => "msg_io",
        };
        ShowError(key, ProfileStore.MaxNameLength);
    }

    private bool NeedProfile(out ProfileInfo? pi)
    {
        pi = SelectedProfile();
        if (_store == null || pi == null || !pi.HasOpt)
        {
            ThemedDialog.Show(this, Strings.Get("t_warn"), Strings.Get(_store == null ? "pr_none" : "pr_select"), DialogKind.Warn);
            return false;
        }
        return true;
    }

    private void RegisterProfiles()
    {
        if (_store == null) { ThemedDialog.Show(this, Strings.Get("t_warn"), Strings.Get("pr_none"), DialogKind.Warn); return; }
        try
        {
            var added = _store.RegisterMissing();
            RefreshProfiles();
            string msg = added.Count == 0 ? Strings.Get("pr_nothing") : string.Format(Strings.Get("pr_registered"), string.Join(", ", added));
            ThemedDialog.Show(this, Strings.Get("t_done"), msg, DialogKind.Info);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { ShowProfileError(ex); }
    }

    private void NewProfile()
    {
        if (!NeedProfile(out var pi) || pi == null || _store == null) return;
        string? name = ThemedDialog.Prompt(this, Strings.Get("pr_new_title"), string.Format(Strings.Get("pr_new_prompt"), pi.Name));
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();
        try
        {
            var files = _store.Clone(pi.Name, name);
            _profiles = _store.Scan();
            FillProfileList(name);
            ThemedDialog.Show(this, Strings.Get("t_done"), string.Format(Strings.Get("pr_created"), name, pi.Name, files.Count), DialogKind.Info);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { ShowProfileError(ex); }
    }

    private void RenameProfile()
    {
        if (!NeedProfile(out var pi) || pi == null || _store == null) return;
        string? name = ThemedDialog.Prompt(this, Strings.Get("pr_rename_title"), string.Format(Strings.Get("pr_rename_prompt"), pi.Name), pi.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();
        try
        {
            string backup = _store.Rename(pi.Name, name);
            _profiles = _store.Scan();
            FillProfileList(name);
            ThemedDialog.Show(this, Strings.Get("t_done"), string.Format(Strings.Get("pr_renamed"), name, backup), DialogKind.Info);
            if (_path != null && !File.Exists(_path))
            {
                Reset();                                 // il file aperto e' stato rinominato insieme al profilo
                _status.Text = Strings.Get("pr_moved");
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { ShowProfileError(ex); }
    }

    // ───────────────────────── scrittura ─────────────────────────

    private static bool TryValue(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);     // solo cifre, 0..2147483647

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static bool IsValidFileBaseName(string n) =>
        n.Length is >= 1 and <= 100 && n.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && !n.EndsWith('.') && !n.EndsWith(' ') && !ReservedNames.Contains(n);

    private void ShowError(string key, params object[] args) =>
        ThemedDialog.Show(this, Strings.Get("t_error"), args.Length == 0 ? Strings.Get(key) : string.Format(Strings.Get(key), args), DialogKind.Error);

    private void Patch()
    {
        if (_path == null)
        {
            ThemedDialog.Show(this, Strings.Get("t_warn"), Strings.Get("msg_select"), DialogKind.Warn);
            return;
        }

        Nfsu1Save s;
        try { s = Nfsu1Save.Load(_path); }      // si rilegge dal disco, come l'originale: il gioco potrebbe averlo cambiato
        catch (InvalidDataException) { ShowError("msg_invalid"); return; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowError("msg_io"); return; }

        // ── valida tutto prima di toccare qualcosa ──
        bool bad = false;

        string moneyText = _change.Text.Trim();
        int money = 0;
        bool moneyOk = moneyText.Length == 0 || TryValue(moneyText, out money);
        Mark(_change, !moneyOk);
        bad |= !moneyOk;

        var stats = new int[Nfsu1Save.AllStats.Length];
        foreach (var st in Nfsu1Save.AllStats)
        {
            bool ok = TryValue(_stats[(int)st].Text.Trim(), out int v);
            stats[(int)st] = v;
            Mark(_stats[(int)st], !ok);
            bad |= !ok;
        }
        if (bad) { ShowError("msg_wrong_value"); return; }

        string? newName = null, newDate = null;
        string nameText = _name.Text.Trim();
        if (nameText != s.Name)
        {
            bool ok = nameText.Length >= 1 && nameText.Length <= s.NameMaxLength && Nfsu1Save.IsPrintableAscii(nameText);
            Mark(_name, !ok);
            if (!ok) { ShowError("msg_bad_name", s.NameMaxLength); return; }
            newName = nameText;
        }
        string dateText = _date.Text.Trim();
        if (dateText != s.Date)
        {
            bool ok = dateText.Length >= 1 && dateText.Length <= s.DateMaxLength && Nfsu1Save.IsPrintableAscii(dateText);
            Mark(_date, !ok);
            if (!ok) { ShowError("msg_bad_date", s.DateMaxLength); return; }
            newDate = dateText;
        }

        string? newPath = null;
        string baseName = _fileName.Text.Trim();
        if (baseName != (Path.GetFileNameWithoutExtension(_path) ?? ""))
        {
            if (!IsValidFileBaseName(baseName)) { Mark(_fileName, true); ShowError("msg_bad_file"); return; }
            newPath = Path.Combine(Path.GetDirectoryName(_path) ?? "", baseName + _origExt);
            if (!string.Equals(newPath, _path, StringComparison.OrdinalIgnoreCase) && File.Exists(newPath))
            {
                Mark(_fileName, true);
                ShowError("msg_file_exists");
                return;
            }
        }

        var levels = new int[CarRows.Length];
        var uniques = new bool[CarRows.Length];
        for (int i = 0; i < CarRows.Length; i++)
        {
            int idx = (int)CarRows[i].Upgrade - 1;                  // l'enum parte da 1
            int level = 0;
            for (int k = 0; k < 4; k++) if (_levels[i][k].Checked) level = k;
            levels[idx] = level;
            uniques[idx] = _uniques[i].Checked;
        }

        var changes = new Nfsu1Changes
        {
            Money = moneyText.Length > 0 ? money : null,
            Name = newName,
            Date = newDate,
            Stats = stats,
            UnlockUniqueCars = _uniqueCars.Checked,
            Events = _radAdvance.Checked ? Nfsu1EventMode.AdvanceTo
                   : _radManual.Checked ? Nfsu1EventMode.Manual
                   : _radComplete.Checked ? Nfsu1EventMode.CompleteAll
                   : Nfsu1EventMode.Keep,
            EventTarget = (int)_eventsNum.Value,
            ManualTable = _manualTable,
            UpgradeLevels = levels,
            UpgradeUnique = uniques,
        };

        try
        {
            s.Apply(changes);
            s.Save(_path, backup: true);        // crea <file>.bak, ricalcola l'hash, scrive
            Nfsu1Save.Load(_path);              // rilegge e verifica dimensione + hash
        }
        catch (InvalidDataException) { ShowError("msg_fail", Path.GetFileName(_path)); return; }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException) { ShowError("msg_wrong_value"); return; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowError("msg_io"); return; }

        string finalPath = _path;
        if (newPath != null)
        {
            try { RenameWithBackup(_path, newPath); finalPath = newPath; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowError("msg_rename_fail"); }
        }

        ThemedDialog.Show(this, Strings.Get("t_done"), Strings.Get("msg_ok"), DialogKind.Info);
        LoadFile(finalPath);
    }

    /// <summary>Rinomina il salvataggio e il suo .bak. Un cambio di sola maiuscola/minuscola passa da un nome temporaneo.</summary>
    private static void RenameWithBackup(string oldPath, string newPath)
    {
        MoveCaseAware(oldPath, newPath);
        if (File.Exists(oldPath + ".bak")) MoveCaseAware(oldPath + ".bak", newPath + ".bak");
    }

    private static void MoveCaseAware(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            string tmp = from + ".rename_tmp";
            File.Move(from, tmp);
            File.Move(tmp, to);
        }
        else File.Move(from, to, overwrite: true);
    }
}
