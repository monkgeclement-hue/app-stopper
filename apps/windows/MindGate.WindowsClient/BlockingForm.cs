namespace MindGate.WindowsClient;

internal sealed class BlockingForm : Form
{
    private readonly Label _appName = new();
    private readonly Label _resetTime = new();

    internal event Action? OpenSettingsRequested;

    internal BlockingForm()
    {
        Text = "MindGate";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(20, 26, 35);
        ForeColor = Color.White;

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(48),
            BackColor = BackColor
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 65));

        var heading = new Label
        {
            Text = "Your time for this app is up",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomCenter,
            Font = new Font("Segoe UI", 26, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 20)
        };
        _appName.Dock = DockStyle.Fill;
        _appName.TextAlign = ContentAlignment.MiddleCenter;
        _appName.Font = new Font("Segoe UI", 16, FontStyle.Regular);
        _appName.Margin = new Padding(0, 0, 0, 12);

        _resetTime.Dock = DockStyle.Fill;
        _resetTime.TextAlign = ContentAlignment.TopCenter;
        _resetTime.Font = new Font("Segoe UI", 12, FontStyle.Regular);
        _resetTime.ForeColor = Color.FromArgb(190, 202, 216);
        _resetTime.Margin = new Padding(0, 0, 0, 28);

        var settingsButton = new Button
        {
            Text = "Open MindGate settings",
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Font = new Font("Segoe UI", 11, FontStyle.Regular),
            Padding = new Padding(16, 8, 16, 8),
            BackColor = Color.FromArgb(74, 133, 230),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0)
        };
        settingsButton.FlatAppearance.BorderSize = 0;
        settingsButton.Click += (_, _) => OpenSettingsRequested?.Invoke();

        var disclosure = new Label
        {
            Text = "MindGate is a self-management aid. On a PC you administer, you can close the app or change its settings.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopCenter,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = Color.FromArgb(160, 173, 188),
            AutoSize = true,
            Margin = new Padding(0, 28, 0, 0)
        };

        content.Controls.Add(new Panel(), 0, 0);
        content.Controls.Add(heading, 0, 1);
        content.Controls.Add(_appName, 0, 2);
        content.Controls.Add(_resetTime, 0, 3);
        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Anchor = AnchorStyles.None,
            AutoSize = true
        };
        bottom.Controls.Add(settingsButton);
        bottom.Controls.Add(disclosure);
        content.Controls.Add(bottom, 0, 4);
        Controls.Add(content);
    }

    internal void ShowFor(string displayName, DateTimeOffset? nextResetUtc, Rectangle bounds)
    {
        _appName.Text = displayName;
        _resetTime.Text = nextResetUtc is null
            ? "Access will return when the cooldown ends."
            : $"Access returns at {nextResetUtc.Value.ToLocalTime():t}.";
        Bounds = bounds;
        if (!Visible)
            Show();
        BringToFront();
    }
}
