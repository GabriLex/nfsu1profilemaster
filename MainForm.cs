using System.Globalization;
using Nfsu1;

namespace NFSU1ProfileMaster;

internal sealed class BufferedPanel : Panel
{
    public BufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}

public sealed class MainForm : Form
{
    private const int W = 540, HeaderH = 100, TabW = 164, PanelW = 512, PanelH = 372, PanelTop = 188;

    private static readonly (string Key, Nfsu1Upgrade Upgrade)[] CarRows =
    {
        ("car_engine", Nfsu1Upgrade.Engine), ("car_transmission", Nfsu1Upgrade.Transmission), ("car_tires", Nfsu1Upgrade.Tires),
        ("car_ecu", Nfsu1Upgrade.Ecu), ("car_turbo", Nfsu1Upgrade.Turbo), ("car_brakes", Nfsu1Upgrade.Brakes),
        ("car_weight", Nfsu1Upgrade.WeightReduction), ("car_suspension", Nfsu1Upgrade.Suspension), ("car_nos", Nfsu1Upgrade.Nos),
    };

    private static readonly Color White = Color.White;     // tutti i testi del menu

    private readonly List<(Control Ctl, string Key, bool Upper)> _i18n = new();
    private readonly Panel[] _tabs = new Panel[3];
    private readonly Button[] _tabButtons = new Button[3];
    private readonly RadioButton[][] _levels = new RadioButton[9][];
    private readonly CheckBox[] _uniques = new CheckBox[9];
    private readonly TextBox[] _stats = new TextBox[17];

    private Nfsu1Save? _save;
    private string? _path;
    private byte[] _manualTable = new byte[Nfsu1Save.EventTableLength];
    private bool _loading;

    private TextBox _file = null!, _name = null!, _date = null!, _bank = null!, _change = null!;
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

        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon; } catch { /* icona opzionale */ }

        BuildUi();
        ApplyLanguage();
        SelectTab(0);
        UpdateEnabled();
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

        // riga del file
        _file = new TextBox { ReadOnly = true };
        StyleBox(_file, 14, 108, 360, White);
        Controls.Add(_file);
        _lamp = new Panel { Left = 384, Top = 115, Width = 14, Height = 14, BackColor = Theme.Bg };
        _lamp.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var br = new SolidBrush(_save != null ? Theme.SelGreen : Theme.Border);
            e.Graphics.FillEllipse(br, 0, 0, 12, 12);
        };
        Controls.Add(_lamp);
        var fileBtn = Theme.MakeButton("", White, Theme.SelDark, Theme.SelRing, Theme.SelDarker, Theme.SelGreen, 12);
        fileBtn.SetBounds(410, 106, 116, 32);
        fileBtn.Click += (_, _) => OpenFileDialog();
        _i18n.Add((fileBtn, "file_btn", true));
        Controls.Add(fileBtn);

        // schede
        string[] tabKeys = { "tab_unlock", "tab_car", "tab_custom" };
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var b = Theme.MakeButton("", White, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 12);
            b.SetBounds(14 + i * (TabW + 6), 148, TabW, 34);
            b.Click += (_, _) => SelectTab(idx);
            _i18n.Add((b, tabKeys[i], true));
            _tabButtons[i] = b;
            Controls.Add(b);

            var p = new Panel { Left = 14, Top = PanelTop, Width = PanelW, Height = PanelH, BackColor = Theme.PanelBg };
            p.Paint += (_, e) => { using var pen = new Pen(Theme.Border); e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1); };
            _tabs[i] = p;
            Controls.Add(p);
        }
        BuildUnlock(_tabs[0]);
        BuildCar(_tabs[1]);
        BuildCustom(_tabs[2]);

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

    private void BuildUnlock(Panel p)
    {
        Lbl(p, "lbl_name", 12, 14, 70);
        _name = new TextBox { ReadOnly = true }; StyleBox(_name, 84, 10, 150, White); p.Controls.Add(_name);
        Lbl(p, "lbl_date", 246, 14, 50);
        _date = new TextBox { ReadOnly = true }; StyleBox(_date, 298, 10, 202, White); p.Controls.Add(_date);
        Lbl(p, "lbl_bank", 12, 52, 70);
        _bank = new TextBox { ReadOnly = true }; StyleBox(_bank, 84, 48, 150, White); p.Controls.Add(_bank);
        Lbl(p, "lbl_change", 246, 52, 100);
        _change = new TextBox { MaxLength = 10 }; StyleBox(_change, 350, 48, 150, White); p.Controls.Add(_change);
        _change.TextChanged += (_, _) => Mark(_change, false);

        _uniqueCars = Check(p, "chk_unique_cars", 12, 92, 460);
        p.Controls.Add(new Panel { Left = 12, Top = 126, Width = 488, Height = 1, BackColor = Theme.LineSep });

        _radNone = Radio(p, "rad_none", 12, 136, 460);
        _radAdvance = Radio(p, "rad_advance", 12, 168, 300);
        _eventsNum = Num(p, 318, 166);
        Lbl(p, "lbl_of", 392, 171, 90);
        _radManual = Radio(p, "rad_manual", 12, 204, 300);
        _manualNum = Num(p, 318, 202);
        _passed = Check(p, "chk_passed", 392, 205, 110);
        _radComplete = Radio(p, "rad_complete", 12, 240, 460);
        _radNone.Checked = true;
        foreach (var r in new[] { _radNone, _radAdvance, _radManual, _radComplete }) r.CheckedChanged += (_, _) => UpdateEnabled();

        _manualNum.ValueChanged += (_, _) => RefreshPassedBox();
        _passed.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            Nfsu1Save.SetEventPassed(_manualTable, (int)_manualNum.Value, _passed.Checked);
        };

        p.Controls.Add(new Panel { Left = 12, Top = 276, Width = 488, Height = 1, BackColor = Theme.LineSep });
        var guide = Theme.MakeButton("", White, Theme.BtnNorm, Theme.Border, Theme.BtnHov, Theme.BorderLt, 11);
        guide.SetBounds(12, 288, 488, 34);
        guide.Click += (_, _) => ThemedDialog.Show(this, Strings.Get("t_guide"), Strings.Get("guide_text"), DialogKind.Info);
        _i18n.Add((guide, "btn_guide", true));
        p.Controls.Add(guide);
        var hint = new Label
        {
            Left = 12, Top = 328, Width = 488, Height = 36, AutoSize = false, ForeColor = White, Font = Theme.Mono(8),
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

        // (statistica, colonna, riga)
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
        foreach (var (ctl, key, upper) in _i18n)
        {
            string t = Strings.Get(key);
            ctl.Text = upper ? t.ToUpperInvariant() : t;
        }
        if (_path == null) _file.Text = Strings.Get("file_none");
    }

    private void SelectTab(int index)
    {
        for (int i = 0; i < 3; i++)
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
        // I campi di testo diventano di sola lettura (il testo resta bianco); i controlli di scelta usano il disegno bianco anche da disabilitati.
        // I contatori restano sempre attivi: contano solo se la relativa opzione e' selezionata.
        _change.ReadOnly = !loaded;
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

    // ───────────────────────── caricamento ─────────────────────────

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
            _name.Text = s.Name; _date.Text = s.Date; _bank.Text = s.Money.ToString(CultureInfo.InvariantCulture);
            _change.Text = ""; Mark(_change, false);
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
    }

    // ───────────────────────── scrittura ─────────────────────────

    private static bool TryValue(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);     // solo cifre, 0..2147483647

    private void Patch()
    {
        if (_path == null)
        {
            ThemedDialog.Show(this, Strings.Get("t_warn"), Strings.Get("msg_select"), DialogKind.Warn);
            return;
        }

        Nfsu1Save s;
        try { s = Nfsu1Save.Load(_path); }      // si rilegge dal disco, come l'originale: il gioco potrebbe averlo cambiato
        catch (InvalidDataException)
        {
            ThemedDialog.Show(this, Strings.Get("t_error"), Strings.Get("msg_invalid"), DialogKind.Error);
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ThemedDialog.Show(this, Strings.Get("t_error"), Strings.Get("msg_io"), DialogKind.Error);
            return;
        }

        // valida tutti i numeri prima di toccare qualcosa
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
        if (bad)
        {
            ThemedDialog.Show(this, Strings.Get("t_error"), Strings.Get("msg_wrong_value"), DialogKind.Error);
            return;
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
        catch (InvalidDataException)
        {
            ThemedDialog.Show(this, Strings.Get("t_error"), string.Format(Strings.Get("msg_fail"), Path.GetFileName(_path)), DialogKind.Error);
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ThemedDialog.Show(this, Strings.Get("t_error"), Strings.Get("msg_io"), DialogKind.Error);
            return;
        }

        ThemedDialog.Show(this, Strings.Get("t_done"), Strings.Get("msg_ok"), DialogKind.Info);
        LoadFile(_path);
    }
}
