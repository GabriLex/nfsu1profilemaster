using System.Drawing.Drawing2D;

namespace NFSU1ProfileMaster;

/// <summary>
/// Windows disegna in grigio di sistema il testo di caselle e pulsanti di scelta disabilitati, ignorando ForeColor.
/// Questi controlli si disegnano da soli: il testo resta bianco anche da disabilitati (cambia solo la casella).
/// </summary>
internal static class ToggleDrawing
{
    public static void Paint(Control c, Graphics g, bool isRadio, bool isChecked)
    {
        g.Clear(c.BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        const int box = 14;
        var r = new Rectangle(1, (c.Height - box) / 2, box, box);
        Color line = c.Enabled ? Color.White : Theme.BorderLt;

        using (var pen = new Pen(c.Enabled ? Theme.BorderLt : Theme.Border, 1.5f))
        {
            if (isRadio) g.DrawEllipse(pen, r); else g.DrawRectangle(pen, r);
        }
        if (isChecked)
        {
            if (isRadio)
            {
                using var br = new SolidBrush(line);
                g.FillEllipse(br, r.X + 4, r.Y + 4, box - 8, box - 8);
            }
            else
            {
                using var pen = new Pen(line, 2f);
                g.DrawLines(pen, new[] { new Point(r.X + 3, r.Y + 7), new Point(r.X + 6, r.Y + 10), new Point(r.X + 11, r.Y + 3) });
            }
        }
        TextRenderer.DrawText(g, c.Text, c.Font, new Rectangle(box + 8, 0, Math.Max(0, c.Width - box - 8), c.Height), Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class WhiteCheckBox : CheckBox
{
    public WhiteCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e) => ToggleDrawing.Paint(this, e.Graphics, false, Checked);
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
}

internal sealed class WhiteRadioButton : RadioButton
{
    public WhiteRadioButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e) => ToggleDrawing.Paint(this, e.Graphics, true, Checked);
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
}
