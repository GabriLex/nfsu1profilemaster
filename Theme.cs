namespace NFSU1ProfileMaster;

/// <summary>Palette ispirata al menu di pausa di NFSU1.</summary>
public static class Theme
{
    public static readonly Color Bg = ColorTranslator.FromHtml("#0A0C12");
    public static readonly Color PanelBg = ColorTranslator.FromHtml("#0D1018");
    public static readonly Color Border = ColorTranslator.FromHtml("#4A5568");
    public static readonly Color BorderLt = ColorTranslator.FromHtml("#8090A8");
    public static readonly Color SelGreen = ColorTranslator.FromHtml("#ff0000");
    public static readonly Color SelDark = ColorTranslator.FromHtml("#050030");
    public static readonly Color SelDarker = ColorTranslator.FromHtml("#4b0000");
    public static readonly Color SelRing = ColorTranslator.FromHtml("#ff0000");
    public static readonly Color TxtWhite = ColorTranslator.FromHtml("#E8EEF8");
    public static readonly Color TxtGrey = ColorTranslator.FromHtml("#ffffff");
    public static readonly Color Gold = ColorTranslator.FromHtml("#775a04");
    public static readonly Color BtnNorm = ColorTranslator.FromHtml("#141820");
    public static readonly Color BtnHov = ColorTranslator.FromHtml("#1E2838");
    public static readonly Color LineSep = ColorTranslator.FromHtml("#252D3A");
    public static readonly Color Danger = ColorTranslator.FromHtml("#0004ff");

    public static Font Impact(float size) => new("Impact", size, FontStyle.Regular, GraphicsUnit.Point);
    public static Font Mono(float size) => new("Consolas", size, FontStyle.Regular, GraphicsUnit.Point);

    public static Button MakeButton(string text, Color fg, Color back, Color border, Color hoverBack, Color hoverBorder, float fontSize = 12)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = back,
            ForeColor = fg,
            Font = Impact(fontSize),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderColor = border;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = hoverBack;
        b.FlatAppearance.MouseDownBackColor = hoverBack;
        b.MouseEnter += (_, _) => b.FlatAppearance.BorderColor = hoverBorder;
        b.MouseLeave += (_, _) => b.FlatAppearance.BorderColor = border;
        return b;
    }
}
