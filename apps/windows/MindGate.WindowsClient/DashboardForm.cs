using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.Json;
using MindGate.Core;

namespace MindGate.WindowsClient;

internal sealed class DashboardForm : Form
{
    private static readonly Color CanvasColor = Color.FromArgb(246, 248, 249);
    private static readonly Color PrimaryColor = Color.FromArgb(21, 151, 126);
    private static readonly Color TextColor = Color.FromArgb(28, 33, 39);
    private static readonly Color MutedColor = Color.FromArgb(137, 149, 160);
    private static readonly string[] AppCategories =
    [
        "Social", "Entertainment", "Web Browser", "Messaging", "Music", "Games",
        "Productivity", "Education", "Shopping", "Travel", "Food", "Utilities", "Other"
    ];

    private readonly LocalStateStore _store = new();
    private readonly List<TrackedRule> _rules;
    private readonly Panel _pageHost = new();
    private readonly Dictionary<string, Panel> _pages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _navigationButtons = new(StringComparer.Ordinal);
    private readonly FlowLayoutPanel _analyticsRows = new();
    private readonly ComboBox _applicationPicker = new();
    private readonly ComboBox _categoryPicker = new();
    private readonly ComboBox _categoryFilter = new();
    private readonly NumericUpDown _allowanceMinutes = new();
    private readonly DateTimePicker _dailyResetTime = new();
    private readonly ListView _rulesView = new();
    private readonly Label _emptyRulesHint = new();
    private readonly Label _analyticsEmptyHint = new();
    private readonly Label _status = new();
    private readonly Label _usedTodayValue = new();
    private readonly Label _todayLimitValue = new();
    private readonly Label _withinLimitValue = new();
    private readonly Label _activeAppsValue = new();
    private readonly Label _blockedAppsValue = new();
    private readonly UsageRing _usageRing = new();
    private readonly UsageMeter _usageMeter = new();
    private readonly Label _monitoringState = new();
    private readonly Button _settingsMonitorButton = new();
    private readonly Button _removeButton = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1_000 };
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly BlockingForm _blockScreen = new();
    private DateTimeOffset _lastSampleUtc = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastSaveUtc = DateTimeOffset.UtcNow;
    private double _fractionalSeconds;
    private string? _blockedRuleId;
    private bool _monitoring = true;
    private bool _stateAvailable = true;
    private bool _exitRequested;

    internal DashboardForm()
    {
        Text = "MindGate";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1_000, 680);
        Size = new Size(1_280, 820);
        Font = new Font("Segoe UI", 10, FontStyle.Regular);

        try
        {
            _rules = _store.Load();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or JsonException)
        {
            _rules = [];
            _stateAvailable = false;
            _monitoring = false;
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "MindGate",
            Visible = true,
            ContextMenuStrip = _trayMenu
        };
        _trayMenu.Items.Add("Open MindGate", null, (_, _) => OpenSettings());
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Exit MindGate", null, (_, _) => ExitApplication());
        _notifyIcon.DoubleClick += (_, _) => OpenSettings();
        _blockScreen.OpenSettingsRequested += OpenSettings;
        BuildInterface();
        RefreshRunningApplications();
        RefreshRulesView();

        _timer.Tick += OnTimerTick;
        if (_stateAvailable)
            _timer.Start();
        UpdateMonitorButton();
        UpdateStatus();
        FormClosing += OnFormClosing;
    }

    private void BuildInterface()
    {
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = CanvasColor,
            Margin = new Padding(0)
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        _pageHost.Dock = DockStyle.Fill;
        _pageHost.BackColor = CanvasColor;
        shell.Controls.Add(_pageHost, 0, 0);

        _status.Dock = DockStyle.Fill;
        _status.AutoSize = true;
        _status.Padding = new Padding(30, 4, 30, 4);
        _status.ForeColor = MutedColor;
        _status.BackColor = CanvasColor;
        _status.Font = new Font("Segoe UI", 8, FontStyle.Regular);
        shell.Controls.Add(_status, 0, 1);
        shell.Controls.Add(BuildBottomNavigation(), 0, 2);
        Controls.Add(shell);

        BuildDashboardPage();
        BuildAppsPage();
        BuildAnalyticsPage();
        BuildSettingsPage();
        ShowPage("dashboard");
    }

    private Control BuildBottomNavigation()
    {
        var navigation = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.White,
            Padding = new Padding(18, 4, 18, 4),
            Margin = new Padding(0)
        };
        for (var column = 0; column < 4; column++)
            navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        navigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        foreach (var item in new[]
                 {
                     (Key: "dashboard", Title: "Dashboard", Icon: "▦"),
                     (Key: "apps", Title: "Apps", Icon: "▣"),
                     (Key: "analytics", Title: "Analytics", Icon: "▥"),
                     (Key: "settings", Title: "Settings", Icon: "⚙")
                 })
        {
            var button = new Button
            {
                Text = $"{item.Icon}\n{item.Title}",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = MutedColor,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Margin = new Padding(4, 0, 4, 0),
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 250, 249);
            button.Click += (_, _) => ShowPage(item.Key);
            _navigationButtons.Add(item.Key, button);
            navigation.Controls.Add(button);
        }
        return navigation;
    }

    private void BuildDashboardPage()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(30, 22, 30, 18),
            BackColor = CanvasColor,
            Margin = new Padding(0)
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 174));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 136));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 158));

        page.Controls.Add(CreateGreetingHeader(), 0, 0);
        page.Controls.Add(CreateTodaySummaryCard(), 0, 1);

        var statusMetrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = CanvasColor,
            Margin = new Padding(0, 10, 0, 12)
        };
        for (var column = 0; column < statusMetrics.ColumnCount; column++)
            statusMetrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / statusMetrics.ColumnCount));
        statusMetrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        statusMetrics.Controls.Add(CreateStatusMetricCard("✓", "Within Limit", _withinLimitValue,
            Color.FromArgb(22, 154, 76), Color.FromArgb(231, 248, 237)), 0, 0);
        statusMetrics.Controls.Add(CreateStatusMetricCard("◷", "Active", _activeAppsValue,
            Color.FromArgb(224, 126, 0), Color.FromArgb(255, 245, 230)), 1, 0);
        statusMetrics.Controls.Add(CreateStatusMetricCard("▣", "Blocked", _blockedAppsValue,
            Color.FromArgb(225, 54, 54), Color.FromArgb(255, 235, 235), last: true), 2, 0);
        page.Controls.Add(statusMetrics, 0, 2);

        page.Controls.Add(CreateDashboardSectionTitle("🔥  Streak"), 0, 3);
        page.Controls.Add(CreateStreakCard(), 0, 4);
        page.Controls.Add(CreateDashboardSectionTitle("⚡  Quick Actions"), 0, 5);

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = CanvasColor,
            Margin = new Padding(0, 8, 0, 0)
        };
        for (var column = 0; column < actions.ColumnCount; column++)
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / actions.ColumnCount));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        actions.Controls.Add(CreateQuickActionCard("ϟ", "Focus Mode", "Choose apps to limit",
            () => ShowPage("apps"), Color.FromArgb(20, 157, 137), Color.FromArgb(229, 248, 244)), 0, 0);
        actions.Controls.Add(CreateQuickActionCard("☾", "Bedtime", "Review monitoring settings",
            () => ShowPage("settings"), Color.FromArgb(70, 101, 157), Color.FromArgb(238, 242, 249)), 1, 0);
        actions.Controls.Add(CreateQuickActionCard("＋", "Add App", "Monitor another app",
            () =>
            {
                ShowPage("apps");
                _applicationPicker.Focus();
            }, Color.FromArgb(216, 132, 0), Color.FromArgb(255, 247, 229), last: true), 2, 0);
        page.Controls.Add(actions, 0, 6);
        AddPage("dashboard", page);
    }

    private static Control CreateGreetingHeader()
    {
        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 14),
            BackColor = CanvasColor
        };
        header.Controls.Add(new Label
        {
            Text = "Good day",
            AutoSize = true,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = TextColor,
            Margin = new Padding(0, 0, 0, 2)
        });
        header.Controls.Add(new Label
        {
            Text = DateTime.Now.ToString("dddd, MMMM d", CultureInfo.CurrentCulture),
            AutoSize = true,
            ForeColor = MutedColor,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            Margin = new Padding(0)
        });
        return header;
    }

    private Control CreateTodaySummaryCard()
    {
        var card = CreateCardPanel();
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, 0, 8);
        card.Padding = new Padding(24, 17, 24, 15);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White,
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var details = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.White,
            Margin = new Padding(0)
        };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        details.Controls.Add(new Label
        {
            Text = "Total Screen Time Today",
            AutoSize = true,
            ForeColor = MutedColor,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            Margin = new Padding(0, 0, 0, 2)
        }, 0, 0);
        _usedTodayValue.AutoSize = true;
        _usedTodayValue.ForeColor = TextColor;
        _usedTodayValue.Font = new Font("Segoe UI", 28, FontStyle.Bold);
        _usedTodayValue.Margin = new Padding(0, 0, 0, 0);
        details.Controls.Add(_usedTodayValue, 0, 1);
        _todayLimitValue.AutoSize = true;
        _todayLimitValue.ForeColor = MutedColor;
        _todayLimitValue.Font = new Font("Segoe UI", 10, FontStyle.Regular);
        _todayLimitValue.Margin = new Padding(0, 0, 0, 9);
        details.Controls.Add(_todayLimitValue, 0, 2);
        _usageMeter.Dock = DockStyle.Fill;
        _usageMeter.Height = 10;
        _usageMeter.Margin = new Padding(0, 1, 0, 0);
        details.Controls.Add(_usageMeter, 0, 3);

        _usageRing.Dock = DockStyle.Fill;
        _usageRing.Margin = new Padding(8, 0, 0, 0);
        layout.Controls.Add(details, 0, 0);
        layout.Controls.Add(_usageRing, 1, 0);
        card.Controls.Add(layout);
        return card;
    }

    private static Control CreateDashboardSectionTitle(string title)
    {
        return new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = TextColor,
            Margin = new Padding(0, 5, 0, 2),
            BackColor = CanvasColor
        };
    }

    private static Control CreateStatusMetricCard(string icon, string title, Label value,
        Color iconColor, Color iconBackground, bool last = false)
    {
        var card = CreateCardPanel();
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, last ? 0 : 10, 0);
        card.Padding = new Padding(8, 8, 8, 8);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.White
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var badge = new Label
        {
            Text = icon,
            Width = 44,
            Height = 44,
            Anchor = AnchorStyles.None,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Symbol", 17, FontStyle.Regular),
            ForeColor = iconColor,
            BackColor = iconBackground,
            Margin = new Padding(0)
        };
        using (var badgePath = new GraphicsPath())
        {
            badgePath.AddEllipse(0, 0, 44, 44);
            badge.Region = new Region(badgePath);
        }
        layout.Controls.Add(badge, 0, 0);
        value.AutoSize = true;
        value.TextAlign = ContentAlignment.MiddleCenter;
        value.Anchor = AnchorStyles.None;
        value.ForeColor = TextColor;
        value.Font = new Font("Segoe UI", 18, FontStyle.Bold);
        value.Margin = new Padding(0);
        layout.Controls.Add(value, 0, 1);
        layout.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Anchor = AnchorStyles.None,
            TextAlign = ContentAlignment.TopCenter,
            ForeColor = MutedColor,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            Margin = new Padding(0)
        }, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private static Control CreateStreakCard()
    {
        var card = CreateCardPanel();
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, 0, 4);
        card.Padding = new Padding(20, 12, 20, 12);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var days = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.White,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        days.Controls.Add(new Label
        {
            Text = "0",
            AutoSize = true,
            Font = new Font("Segoe UI", 28, FontStyle.Bold),
            ForeColor = Color.FromArgb(239, 154, 0),
            Margin = new Padding(0)
        });
        days.Controls.Add(new Label
        {
            Text = "days",
            AutoSize = true,
            Font = new Font("Segoe UI", 8, FontStyle.Regular),
            ForeColor = MutedColor,
            Margin = new Padding(0, -2, 0, 0)
        });
        layout.Controls.Add(days, 0, 0);
        var copy = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.White,
            Padding = new Padding(0, 12, 0, 0),
            Margin = new Padding(0)
        };
        copy.Controls.Add(new Label
        {
            Text = "Longest streak: 0 days",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            ForeColor = MutedColor,
            Margin = new Padding(0, 0, 0, 3)
        });
        copy.Controls.Add(new Label
        {
            Text = "Start your streak today",
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = MutedColor,
            Margin = new Padding(0)
        });
        layout.Controls.Add(copy, 1, 0);
        card.Controls.Add(layout);
        return card;
    }

    private static Control CreateQuickActionCard(string icon, string title, string caption, Action onClick,
        Color iconColor, Color iconBackground, bool last = false)
    {
        var card = CreateCardPanel();
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, last ? 0 : 10, 0);
        card.Padding = new Padding(8, 12, 8, 8);
        card.Cursor = Cursors.Hand;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.White,
            Cursor = Cursors.Hand
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var badge = new Label
        {
            Text = icon,
            Width = 44,
            Height = 44,
            Anchor = AnchorStyles.None,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Symbol", 19, FontStyle.Regular),
            ForeColor = iconColor,
            BackColor = iconBackground,
            Margin = new Padding(0)
        };
        using (var badgePath = CreateRoundedPath(new Rectangle(0, 0, 44, 44), 12))
            badge.Region = new Region(badgePath);
        layout.Controls.Add(badge, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Anchor = AnchorStyles.None,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = TextColor,
            Margin = new Padding(0)
        }, 0, 1);
        layout.Controls.Add(new Label
        {
            Text = caption,
            AutoSize = true,
            Anchor = AnchorStyles.None,
            TextAlign = ContentAlignment.TopCenter,
            Font = new Font("Segoe UI", 8, FontStyle.Regular),
            ForeColor = MutedColor,
            Margin = new Padding(0)
        }, 0, 2);
        card.Controls.Add(layout);
        void HandleClick(object? sender, EventArgs args) => onClick();
        card.Click += HandleClick;
        layout.Click += HandleClick;
        foreach (Control child in layout.Controls)
        {
            child.Cursor = Cursors.Hand;
            child.Click += HandleClick;
        }
        return card;
    }

    private void BuildAppsPage()
    {
        var page = CreatePageLayout(4);
        page.Controls.Add(CreatePageHeader("My Apps", "Choose what to monitor and set a daily time allowance."), 0, 0);

        _applicationPicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _applicationPicker.Width = 230;
        _categoryPicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _categoryPicker.Width = 155;
        _categoryPicker.Items.AddRange(AppCategories.Cast<object>().ToArray());
        _categoryPicker.SelectedItem = "Other";
        _allowanceMinutes.Minimum = 1;
        _allowanceMinutes.Maximum = 1_440;
        _allowanceMinutes.Value = 60;
        _allowanceMinutes.Width = 120;
        _dailyResetTime.Format = DateTimePickerFormat.Time;
        _dailyResetTime.ShowUpDown = true;
        _dailyResetTime.Value = DateTime.Today.AddHours(4);
        _dailyResetTime.Width = 112;

        var setupCard = CreateCardPanel();
        setupCard.Dock = DockStyle.Top;
        setupCard.AutoSize = true;
        setupCard.Padding = new Padding(17);
        var setupLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White
        };
        setupLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        setupLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        setupLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        setupLayout.Controls.Add(CreateSectionTitle("Add an app", "The app must be open so MindGate can find it."), 0, 0);
        var fields = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = Color.White,
            Margin = new Padding(0)
        };
        fields.Controls.Add(CreateInputField("Running app", _applicationPicker, 240));
        fields.Controls.Add(CreateInputField("Category", _categoryPicker, 165));
        fields.Controls.Add(CreateInputField("Daily limit (minutes)", _allowanceMinutes, 135));
        fields.Controls.Add(CreateInputField("Reset time", _dailyResetTime, 125));
        var addButton = CreateStyledButton("Add app limit", primary: true);
        addButton.Margin = new Padding(0, 20, 6, 0);
        addButton.Click += (_, _) => AddRule();
        fields.Controls.Add(addButton);
        var refreshButton = CreateStyledButton("Refresh apps", primary: false);
        refreshButton.Margin = new Padding(0, 20, 0, 0);
        refreshButton.Click += (_, _) => RefreshRunningApplications();
        fields.Controls.Add(refreshButton);
        setupLayout.Controls.Add(fields, 0, 1);
        setupCard.Controls.Add(setupLayout);
        page.Controls.Add(setupCard, 0, 1);

        page.Controls.Add(CreateSectionTitle("Your limits", "Select a row to remove a limit when it is not active."), 0, 2);
        _rulesView.Dock = DockStyle.Fill;
        _rulesView.View = View.Details;
        _rulesView.FullRowSelect = true;
        _rulesView.GridLines = false;
        _rulesView.MultiSelect = false;
        _rulesView.BorderStyle = BorderStyle.None;
        _rulesView.BackColor = Color.White;
        _rulesView.Font = new Font("Segoe UI", 9, FontStyle.Regular);
        _rulesView.SelectedIndexChanged += (_, _) => UpdateGuardedControls();
        _rulesView.Columns.Add("App", 170);
        _rulesView.Columns.Add("Category", 120);
        _rulesView.Columns.Add("Daily limit", 95);
        _rulesView.Columns.Add("Used", 90);
        _rulesView.Columns.Add("Left", 90);
        _rulesView.Columns.Add("Status", 100);
        _rulesView.Columns.Add("Next reset", 110);

        var rulesContainer = CreateCardPanel();
        rulesContainer.Dock = DockStyle.Fill;
        var rulesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12),
            BackColor = Color.White
        };
        rulesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rulesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rulesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 8)
        };
        _categoryFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _categoryFilter.Width = 180;
        _categoryFilter.Items.Add("All categories");
        _categoryFilter.Items.AddRange(AppCategories.Cast<object>().ToArray());
        _categoryFilter.SelectedIndex = 0;
        _categoryFilter.SelectedIndexChanged += (_, _) => RefreshRulesView();
        toolbar.Controls.Add(_categoryFilter);
        _removeButton.Text = "Remove selected limit";
        _removeButton.AutoSize = true;
        _removeButton.Click += (_, _) => RemoveSelectedRule();
        toolbar.Controls.Add(_removeButton);
        rulesLayout.Controls.Add(toolbar, 0, 0);
        var listPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        listPanel.Controls.Add(_rulesView);
        _emptyRulesHint.Text = "No limits yet\nChoose a running app above to create your first daily limit.";
        _emptyRulesHint.Dock = DockStyle.Fill;
        _emptyRulesHint.TextAlign = ContentAlignment.MiddleCenter;
        _emptyRulesHint.Font = new Font("Segoe UI", 11, FontStyle.Regular);
        _emptyRulesHint.ForeColor = MutedColor;
        _emptyRulesHint.BackColor = Color.White;
        listPanel.Controls.Add(_emptyRulesHint);
        rulesLayout.Controls.Add(listPanel, 0, 1);
        rulesContainer.Controls.Add(rulesLayout);
        page.Controls.Add(rulesContainer, 0, 3);
        AddPage("apps", page);
    }

    private void BuildAnalyticsPage()
    {
        var page = CreatePageLayout(3);
        page.Controls.Add(CreatePageHeader("Analytics", "Review use during the current limit cycle."), 0, 0);
        var note = CreateInfoCard(
            "Current-cycle view",
            "This view shows the usage MindGate has recorded for your active limits. Weekly history will appear after daily usage history is added.");
        note.Dock = DockStyle.Top;
        page.Controls.Add(note, 0, 1);

        var usageCard = CreateCardPanel();
        usageCard.Dock = DockStyle.Fill;
        var usageLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(18),
            BackColor = Color.White
        };
        usageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        usageLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        usageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        usageLayout.Controls.Add(CreateSectionTitle("Usage by app", "Progress is measured against each app’s own limit."), 0, 0);
        var rowsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        _analyticsRows.Dock = DockStyle.Fill;
        _analyticsRows.FlowDirection = FlowDirection.TopDown;
        _analyticsRows.WrapContents = false;
        _analyticsRows.AutoScroll = true;
        _analyticsRows.BackColor = Color.White;
        _analyticsRows.SizeChanged += (_, _) =>
        {
            foreach (Control row in _analyticsRows.Controls)
                row.Width = Math.Max(420, _analyticsRows.ClientSize.Width - 28);
        };
        rowsPanel.Controls.Add(_analyticsRows);
        _analyticsEmptyHint.Text = "No usage to show yet. Add an app limit and use that app to begin tracking.";
        _analyticsEmptyHint.Dock = DockStyle.Fill;
        _analyticsEmptyHint.TextAlign = ContentAlignment.MiddleCenter;
        _analyticsEmptyHint.ForeColor = MutedColor;
        _analyticsEmptyHint.BackColor = Color.White;
        rowsPanel.Controls.Add(_analyticsEmptyHint);
        usageLayout.Controls.Add(rowsPanel, 0, 1);
        usageCard.Controls.Add(usageLayout);
        page.Controls.Add(usageCard, 0, 2);
        AddPage("analytics", page);
    }

    private void BuildSettingsPage()
    {
        var page = CreatePageLayout(5);
        page.Controls.Add(CreatePageHeader("Settings", "Make sure MindGate is monitoring the way you expect."), 0, 0);
        var monitoringCard = CreateCardPanel();
        monitoringCard.Dock = DockStyle.Top;
        monitoringCard.Padding = new Padding(18);
        var monitoringLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.White
        };
        monitoringLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        monitoringLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        monitoringLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        monitoringLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        monitoringLayout.Controls.Add(CreateSectionTitle("Monitoring", "Pause or resume foreground-use tracking."), 0, 0);
        _monitoringState.AutoSize = true;
        _monitoringState.ForeColor = MutedColor;
        _monitoringState.Margin = new Padding(0, 8, 0, 0);
        monitoringLayout.Controls.Add(_monitoringState, 0, 1);
        _settingsMonitorButton.AutoSize = true;
        _settingsMonitorButton.Height = 38;
        _settingsMonitorButton.Padding = new Padding(12, 3, 12, 3);
        StyleButton(_settingsMonitorButton, primary: false);
        _settingsMonitorButton.Click += (_, _) => ToggleMonitoring();
        monitoringLayout.Controls.Add(_settingsMonitorButton, 1, 0);
        monitoringCard.Controls.Add(monitoringLayout);
        page.Controls.Add(monitoringCard, 0, 1);

        var reminders = CreateInfoCard("Reminders", "MindGate warns at 30, 20, and 10 minutes remaining when those thresholds fit the chosen limit.");
        reminders.Dock = DockStyle.Top;
        page.Controls.Add(reminders, 0, 2);
        var privacy = CreateInfoCard("On-device data", "Your app rules and usage counters are saved locally. MindGate does not need an account or cloud service for this prototype.");
        privacy.Dock = DockStyle.Top;
        page.Controls.Add(privacy, 0, 3);
        var enforcement = CreateInfoCard("About blocking", "Windows blocking is an early user-mode proof. A person who administers this PC can close MindGate or change its local files.");
        enforcement.Dock = DockStyle.Top;
        page.Controls.Add(enforcement, 0, 4);
        AddPage("settings", page);
    }

    private static TableLayoutPanel CreatePageLayout(int rowCount)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 1,
            RowCount = rowCount,
            Padding = new Padding(30, 24, 30, 24),
            BackColor = CanvasColor,
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < rowCount - 1; row++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return layout;
    }

    private static Control CreatePageHeader(string title, string description)
    {
        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 18),
            BackColor = CanvasColor
        };
        header.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI", 23, FontStyle.Bold),
            ForeColor = TextColor,
            Margin = new Padding(0, 0, 0, 3)
        });
        header.Controls.Add(new Label
        {
            Text = description,
            AutoSize = true,
            ForeColor = MutedColor,
            Margin = new Padding(0)
        });
        return header;
    }

    private static Control CreateSectionTitle(string title, string description)
    {
        var section = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.White,
            Margin = new Padding(0)
        };
        section.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = TextColor,
            Margin = new Padding(0, 0, 0, 2)
        });
        section.Controls.Add(new Label
        {
            Text = description,
            AutoSize = true,
            ForeColor = MutedColor,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            Margin = new Padding(0)
        });
        return section;
    }

    private static Panel CreateCardPanel() => new RoundedCardPanel
    {
        BackColor = Color.White,
        Margin = new Padding(0, 0, 0, 16)
    };

    private static Control CreateInputField(string label, Control input, int width)
    {
        var field = new TableLayoutPanel
        {
            Width = width,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 12, 8)
        };
        field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        field.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            ForeColor = MutedColor,
            Font = new Font("Segoe UI", 8, FontStyle.Regular),
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);
        input.Margin = new Padding(0);
        input.Width = width - 4;
        field.Controls.Add(input, 0, 1);
        return field;
    }

    private static Button CreateStyledButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 38,
            Padding = new Padding(12, 3, 12, 3),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? PrimaryColor : Color.FromArgb(242, 243, 250),
            ForeColor = primary ? Color.White : TextColor,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        StyleButton(button, primary);
        return button;
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = primary ? PrimaryColor : Color.FromArgb(242, 243, 250);
        button.ForeColor = primary ? Color.White : TextColor;
        button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = primary
            ? Color.FromArgb(80, 72, 195)
            : Color.FromArgb(230, 231, 242);
    }

    private static Control CreateInfoCard(string title, string description)
    {
        var card = CreateCardPanel();
        card.AutoSize = true;
        card.Padding = new Padding(18);
        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.White
        };
        content.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = TextColor,
            Margin = new Padding(0, 0, 0, 5)
        });
        content.Controls.Add(new Label
        {
            Text = description,
            AutoSize = true,
            MaximumSize = new Size(820, 0),
            ForeColor = MutedColor,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            Margin = new Padding(0)
        });
        card.Controls.Add(content);
        return card;
    }

    private void AddPage(string key, Control page)
    {
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        _pages.Add(key, (Panel)page);
        _pageHost.Controls.Add(page);
    }

    private void ShowPage(string key)
    {
        foreach (var (pageKey, page) in _pages)
            page.Visible = pageKey == key;
        if (_pages.TryGetValue(key, out var selectedPage))
            selectedPage.BringToFront();
        foreach (var (navKey, button) in _navigationButtons)
        {
            var selected = navKey == key;
            button.BackColor = selected ? Color.FromArgb(243, 250, 248) : Color.White;
            button.ForeColor = selected ? PrimaryColor : MutedColor;
            button.Font = new Font("Segoe UI", 9, selected ? FontStyle.Bold : FontStyle.Regular);
        }
    }

    private void RefreshUsagePanels()
    {
        _analyticsRows.SuspendLayout();
        _analyticsRows.Controls.Clear();
        var enabledRules = _rules.Where(item => item.Rule.Enabled).ToArray();
        _analyticsEmptyHint.Visible = enabledRules.Length == 0;
        if (enabledRules.Length > 0)
        {
            foreach (var tracked in enabledRules.OrderByDescending(item => item.State.UsedSeconds))
                _analyticsRows.Controls.Add(CreateUsageRow(tracked));
        }
        _analyticsRows.ResumeLayout();
    }

    private Control CreateUsageRow(TrackedRule tracked)
    {
        var row = new Panel
        {
            Dock = DockStyle.Top,
            Width = Math.Max(420, _analyticsRows.ClientSize.Width - 28),
            Height = 66,
            BackColor = Color.White,
            Margin = new Padding(0, 6, 0, 6)
        };
        var name = new Label
        {
            Text = $"{tracked.Rule.Target.DisplayName}  ·  {tracked.Category}",
            AutoSize = true,
            Location = new Point(0, 2),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = TextColor
        };
        var amount = new Label
        {
            Text = $"{FormatDuration(tracked.State.UsedSeconds)} / {FormatDuration(tracked.Rule.AllowanceSeconds)}",
            AutoSize = true,
            Location = new Point(0, 25),
            Font = new Font("Segoe UI", 8, FontStyle.Regular),
            ForeColor = MutedColor
        };
        var progress = new ProgressBar
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            Location = new Point(0, 47),
            Height = 8,
            Minimum = 0,
            Maximum = 100,
            Width = 650
        };
        progress.Value = GetUsagePercent(tracked);
        row.Controls.Add(name);
        row.Controls.Add(amount);
        row.Controls.Add(progress);
        row.Resize += (_, _) => progress.Width = Math.Max(100, row.ClientSize.Width - 8);
        progress.Width = Math.Max(100, row.ClientSize.Width - 8);
        return row;
    }

    private static int GetUsagePercent(TrackedRule tracked) =>
        tracked.Rule.AllowanceSeconds <= 0
            ? 0
            : Math.Clamp((int)Math.Round(100d * tracked.State.UsedSeconds / tracked.Rule.AllowanceSeconds), 0, 100);

    private void RefreshRunningApplications()
    {
        var previousPath = (_applicationPicker.SelectedItem as RunningApplication)?.ExecutablePath;
        var windowsDirectory = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.Windows))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var apps = new Dictionary<string, RunningApplication>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == Environment.ProcessId || process.MainWindowHandle == 0)
                        continue;
                    var path = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(path))
                        continue;
                    path = Path.GetFullPath(path);
                    if (path.StartsWith(windowsDirectory, StringComparison.OrdinalIgnoreCase))
                        continue;
                    apps.TryAdd(path, new RunningApplication(process.ProcessName, path));
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                                   System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    // Ignore processes that exited or whose executable path is not readable.
                }
            }
        }

        _applicationPicker.DataSource = apps.Values.OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (previousPath is null)
            return;
        for (var index = 0; index < _applicationPicker.Items.Count; index++)
        {
            if (_applicationPicker.Items[index] is RunningApplication app && PathsEqual(app.ExecutablePath, previousPath))
            {
                _applicationPicker.SelectedIndex = index;
                break;
            }
        }
    }

    private void AddRule()
    {
        if (_applicationPicker.SelectedItem is not RunningApplication app)
        {
            MessageBox.Show(this, "Start the app first, then refresh the running-app list and select it.",
                "Choose an app", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_rules.Any(item => PathsEqual(item.Rule.Target.SelectionRef, app.ExecutablePath)))
        {
            MessageBox.Show(this, "A limit for this app already exists.", "Limit already exists",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var allowance = checked((int)_allowanceMinutes.Value * 60);
        var thresholds = new[] { 1_800, 1_200, 600 }.Where(value => value < allowance).ToArray();
        if (thresholds.Length == 0)
            thresholds = [Math.Max(1, allowance / 2)];

        var timeZoneId = TimeZoneInfo.Local.Id;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out var ianaTimeZoneId) && ianaTimeZoneId is not null)
            timeZoneId = ianaTimeZoneId;

        var rule = new AppRule
        {
            SchemaVersion = "1.0",
            RuleId = Guid.NewGuid().ToString("D"),
            Target = new AppTarget { Platform = "windows", SelectionRef = app.ExecutablePath, DisplayName = app.Name },
            AllowanceSeconds = allowance,
            WarningThresholdsSeconds = thresholds,
            ResetPolicy = new ResetPolicy
            {
                Mode = "daily",
                LocalTime = _dailyResetTime.Value.ToString("HH:mm", CultureInfo.InvariantCulture),
                TimeZone = timeZoneId
            },
            Enabled = true
        };

        AppRuleValidator.EnsureValid(rule);
        _rules.Add(new TrackedRule
        {
            Rule = rule,
            State = new UsageCycleState(),
            Category = _categoryPicker.SelectedItem as string ?? "Other"
        });
        SaveImmediately();
        RefreshRulesView();
        _status.Text = $"Added a local limit for {app.Name}. The app must be running for it to appear in the picker.";
    }

    private void RemoveSelectedRule()
    {
        if (_rulesView.SelectedIndices.Count == 0)
            return;
        var selectedId = _rulesView.SelectedItems[0].Tag as string;
        var index = _rules.FindIndex(item => item.Rule.RuleId == selectedId);
        if (index < 0)
            return;

        var removed = _rules[index];
        if (IsCurrentlyBlocked(removed, DateTimeOffset.UtcNow))
        {
            _status.Text = $"{removed.Rule.Target.DisplayName} is blocked. Its limit unlocks at the next reset.";
            UpdateGuardedControls();
            return;
        }

        _rules.RemoveAt(index);
        if (_blockedRuleId == removed.Rule.RuleId)
        {
            _blockedRuleId = null;
            _blockScreen.Hide();
        }
        SaveImmediately();
        RefreshRulesView();
        _status.Text = $"Removed the local limit for {removed.Rule.Target.DisplayName}.";
    }

    private void ToggleMonitoring()
    {
        if (!_stateAvailable)
            return;
        if (_monitoring && HasActiveBlock(DateTimeOffset.UtcNow))
        {
            _status.Text = "Monitoring cannot be paused while an app is blocked. The block ends at its scheduled reset.";
            UpdateGuardedControls();
            return;
        }
        _monitoring = !_monitoring;
        _lastSampleUtc = DateTimeOffset.UtcNow;
        _fractionalSeconds = 0;
        if (!_monitoring)
            _blockScreen.Hide();
        UpdateMonitorButton();
        UpdateStatus();
    }

    private void UpdateMonitorButton()
    {
        _settingsMonitorButton.Text = _monitoring ? "Pause monitoring" : "Resume monitoring";
        _monitoringState.Text = _monitoring ? "Monitoring is active." : "Monitoring is paused.";
        UpdateGuardedControls();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_monitoring || !_stateAvailable)
            return;

        try
        {
            var now = DateTimeOffset.UtcNow;
            var elapsed = (now - _lastSampleUtc).TotalSeconds;
            _lastSampleUtc = now;
            if (elapsed < 0 || elapsed > 30)
                elapsed = 0; // Do not charge a long pause or sleep as foreground use.

            var foreground = ForegroundWindowReader.Read();
            var foregroundPath = foreground?.ExecutablePath;
            var overlayHasFocus = _blockScreen.Visible && ForegroundWindowReader.IsForeground(_blockScreen.Handle);
            var blockedRule = _rules.FirstOrDefault(rule => rule.Rule.RuleId == _blockedRuleId);
            var targetStillForeground = blockedRule is not null && foregroundPath is not null &&
                PathsEqual(foregroundPath, blockedRule.Rule.Target.SelectionRef);
            if (_blockScreen.Visible && !overlayHasFocus && !targetStillForeground)
            {
                _blockScreen.Hide();
                _blockedRuleId = null;
            }

            var matchedRule = foregroundPath is null
                ? null
                : _rules.FirstOrDefault(rule => PathsEqual(foregroundPath, rule.Rule.Target.SelectionRef));
            var wholeSeconds = 0;
            if (matchedRule is not null && elapsed > 0)
            {
                _fractionalSeconds += elapsed;
                wholeSeconds = (int)_fractionalSeconds;
                _fractionalSeconds -= wholeSeconds;
            }
            else
            {
                _fractionalSeconds = 0;
            }

            var persistSoon = false;
            foreach (var tracked in _rules)
            {
                var previousUsed = tracked.State.UsedSeconds;
                var seconds = ReferenceEquals(tracked, matchedRule) ? wholeSeconds : 0;
                var update = UsageBudgetEngine.Consume(tracked.Rule, tracked.State, seconds, now);
                tracked.State = update.State;
                persistSoon |= update.CycleReset || previousUsed != update.State.UsedSeconds;

                foreach (var threshold in update.NewlyReachedWarningThresholdsSeconds)
                    ShowWarning(tracked.Rule.Target.DisplayName, threshold);

                if (update.IsBlocked && matchedRule == tracked)
                {
                    _blockedRuleId = tracked.Rule.RuleId;
                    var bounds = foreground is null
                        ? Screen.PrimaryScreen?.Bounds ?? Bounds
                        : Screen.FromHandle(foreground.Handle).Bounds;
                    _blockScreen.ShowFor(tracked.Rule.Target.DisplayName, update.NextResetAtUtc, bounds);
                }
            }

            if (persistSoon && now - _lastSaveUtc >= TimeSpan.FromSeconds(10))
                SaveImmediately();
            RefreshRulesView();
            UpdateStatus();
        }
        catch (Exception exception)
        {
            _timer.Stop();
            _monitoring = false;
            _blockScreen.Hide();
            UpdateMonitorButton();
            _status.Text = $"Monitoring stopped to protect your data: {exception.Message}";
        }
    }

    private void ShowWarning(string appName, int remainingSeconds)
    {
        var remaining = TimeSpan.FromSeconds(remainingSeconds);
        var message = remaining.TotalMinutes >= 1
            ? $"{Math.Floor(remaining.TotalMinutes):0} minute(s) remaining."
            : $"{remainingSeconds} second(s) remaining.";
        _notifyIcon.BalloonTipTitle = $"MindGate — {appName}";
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(2_500);
        System.Media.SystemSounds.Exclamation.Play();
        _status.Text = $"{appName}: {message}";
    }

    private void RefreshRulesView()
    {
        var selectedRuleId = _rulesView.SelectedItems.Count == 0
            ? null
            : _rulesView.SelectedItems[0].Tag as string;
        _rulesView.BeginUpdate();
        _rulesView.Items.Clear();
        var selectedCategory = _categoryFilter.SelectedItem as string;
        var visibleRules = _rules.Where(item =>
            selectedCategory is null or "All categories" || item.Category == selectedCategory).ToArray();
        foreach (var tracked in visibleRules)
        {
            var state = tracked.State;
            var remainingSeconds = Math.Max(0, tracked.Rule.AllowanceSeconds - state.UsedSeconds);
            var status = !tracked.Rule.Enabled ? "Off" : remainingSeconds == 0 ? "Blocked" : "Available";
            var reset = state.CycleEndUtc?.ToLocalTime().ToString("t", CultureInfo.CurrentCulture) ?? "Starting…";
            var row = new ListViewItem(tracked.Rule.Target.DisplayName);
            row.SubItems.Add(tracked.Category);
            row.SubItems.Add(FormatDuration(tracked.Rule.AllowanceSeconds));
            row.SubItems.Add(FormatDuration(state.UsedSeconds));
            row.SubItems.Add(FormatDuration(remainingSeconds));
            row.SubItems.Add(status);
            row.SubItems.Add(reset);
            row.Tag = tracked.Rule.RuleId;
            _rulesView.Items.Add(row);
            if (tracked.Rule.RuleId == selectedRuleId)
            {
                row.Selected = true;
                row.Focused = true;
            }
        }
        _rulesView.EndUpdate();
        _emptyRulesHint.Text = _rules.Count == 0
            ? "No limits yet\nChoose a running app above to create your first daily limit."
            : "No apps in this category yet. Choose another category or add a limit.";
        _emptyRulesHint.Visible = visibleRules.Length == 0;
        UpdateDashboardSummary();
        RefreshUsagePanels();
        UpdateGuardedControls();
    }

    private void UpdateDashboardSummary()
    {
        var trackedRules = _rules.Where(item => item.Rule.Enabled).ToArray();
        var usedSeconds = trackedRules.Sum(item => (long)item.State.UsedSeconds);
        var allowedSeconds = trackedRules.Sum(item => (long)item.Rule.AllowanceSeconds);
        var now = DateTimeOffset.UtcNow;
        var blockedCount = trackedRules.Count(item => IsCurrentlyBlocked(item, now));
        var percentage = allowedSeconds <= 0
            ? 0
            : Math.Clamp((int)Math.Round(100d * usedSeconds / allowedSeconds), 0, 100);

        _usedTodayValue.Text = FormatCompactDuration((int)Math.Min(int.MaxValue, usedSeconds));
        _todayLimitValue.Text = $"of {FormatCompactDuration((int)Math.Min(int.MaxValue, allowedSeconds))} daily limit";
        _withinLimitValue.Text = (trackedRules.Length - blockedCount).ToString(CultureInfo.CurrentCulture);
        _activeAppsValue.Text = trackedRules.Length.ToString(CultureInfo.CurrentCulture);
        _blockedAppsValue.Text = blockedCount.ToString(CultureInfo.CurrentCulture);
        _usageRing.Percent = percentage;
        _usageMeter.Percent = percentage;
    }

    private void UpdateGuardedControls()
    {
        if (_rulesView.IsDisposed || _removeButton.IsDisposed || _settingsMonitorButton.IsDisposed)
            return;

        var now = DateTimeOffset.UtcNow;
        var activeBlock = HasActiveBlock(now);
        var canToggleMonitoring = _stateAvailable && (!_monitoring || !activeBlock);
        _settingsMonitorButton.Enabled = canToggleMonitoring;

        var selectedId = _rulesView.SelectedItems.Count == 0
            ? null
            : _rulesView.SelectedItems[0].Tag as string;
        var selectedRule = selectedId is null
            ? null
            : _rules.FirstOrDefault(item => item.Rule.RuleId == selectedId);
        _removeButton.Enabled = _stateAvailable && selectedRule is not null &&
            !IsCurrentlyBlocked(selectedRule, now);
    }

    private bool HasActiveBlock(DateTimeOffset now) => _rules.Any(rule => IsCurrentlyBlocked(rule, now));

    private static bool IsCurrentlyBlocked(TrackedRule tracked, DateTimeOffset now)
    {
        var rule = tracked.Rule;
        var state = tracked.State;
        if (!rule.Enabled || state.UsedSeconds < rule.AllowanceSeconds)
            return false;

        if (rule.ResetPolicy.Mode == "daily")
            return state.CycleEndUtc is { } cycleEnd && now.ToUniversalTime() < cycleEnd;

        return state.ExhaustedAtUtc is { } exhaustedAt &&
            now.ToUniversalTime() < exhaustedAt.AddSeconds(rule.ResetPolicy.CooldownSeconds!.Value);
    }

    private void UpdateStatus()
    {
        if (!_stateAvailable)
        {
            _status.Text = "Local rules could not be read. Monitoring is paused to avoid overwriting saved limits.";
            return;
        }
        _status.Text = _monitoring
            ? "MindGate is active. Only selected-app foreground time is counted; your rules and counters stay on this device."
            : "MindGate is paused. App use is not being counted while paused.";
    }

    private static string FormatDuration(int seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes:00}m";
        if (duration.TotalMinutes >= 1)
            return $"{duration.Minutes}m {duration.Seconds:00}s";
        return $"{duration.Seconds}s";
    }

    private static string FormatCompactDuration(int seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        return $"{(int)duration.TotalMinutes}m";
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private void SaveImmediately()
    {
        try
        {
            _store.Save(_rules);
            _lastSaveUtc = DateTimeOffset.UtcNow;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _stateAvailable = false;
            _monitoring = false;
            _timer.Stop();
            _blockScreen.Hide();
            UpdateMonitorButton();
            _status.Text = $"Could not save local state; monitoring paused. {exception.Message}";
        }
    }

    private void OpenSettings()
    {
        _blockScreen.Hide();
        _blockedRuleId = null;
        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
        Activate();
        ShowPage("settings");
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            if (_stateAvailable)
                SaveImmediately();
            Hide();
            _notifyIcon.BalloonTipTitle = "MindGate is still running";
            _notifyIcon.BalloonTipText = "Monitoring continues in the system tray. Double-click the icon to open MindGate.";
            _notifyIcon.ShowBalloonTip(2_500);
            return;
        }

        _timer.Stop();
        if (_stateAvailable)
            SaveImmediately();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _trayMenu.Dispose();
        _blockScreen.Dispose();
    }

    private sealed class RoundedCardPanel : Panel
    {
        public RoundedCardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (Width < 2 || Height < 2)
                return;
            using var path = CreateRoundedPath(new Rectangle(0, 0, Width, Height), 17);
            var previous = Region;
            Region = new Region(path);
            previous?.Dispose();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? CanvasColor);
            using var path = CreateRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 17);
            using var fill = new SolidBrush(Color.White);
            using var border = new Pen(Color.FromArgb(235, 239, 241));
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
        }
    }

    private sealed class UsageRing : Control
    {
        private int _percent;

        public UsageRing()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.White;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Percent
        {
            get => _percent;
            set
            {
                var next = Math.Clamp(value, 0, 100);
                if (_percent == next)
                    return;
                _percent = next;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var diameter = Math.Max(36, Math.Min(ClientSize.Width, ClientSize.Height) - 14);
            var bounds = new RectangleF(
                (ClientSize.Width - diameter) / 2f,
                (ClientSize.Height - diameter) / 2f,
                diameter,
                diameter);
            using var track = new Pen(Color.FromArgb(190, 231, 223), 7);
            using var progress = new Pen(PrimaryColor, 7);
            e.Graphics.DrawArc(track, bounds, -90, 360);
            if (_percent > 0)
                e.Graphics.DrawArc(progress, bounds, -90, 3.6f * _percent);
            using var font = new Font("Segoe UI", 12, FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics, $"{_percent}%", font, ClientRectangle,
                Color.FromArgb(20, 145, 121), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private sealed class UsageMeter : Control
    {
        private int _percent;

        public UsageMeter()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.White;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Percent
        {
            get => _percent;
            set
            {
                var next = Math.Clamp(value, 0, 100);
                if (_percent == next)
                    return;
                _percent = next;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var height = Math.Min(10, Math.Max(5, ClientSize.Height - 2));
            var track = new RectangleF(0, (ClientSize.Height - height) / 2f, ClientSize.Width, height);
            using var trackPath = CreateRoundedPath(Rectangle.Round(track), height / 2);
            using var trackBrush = new SolidBrush(Color.FromArgb(235, 240, 242));
            e.Graphics.FillPath(trackBrush, trackPath);
            if (_percent <= 0 || track.Width <= 0)
                return;
            var fill = track;
            fill.Width = Math.Max(height, track.Width * _percent / 100f);
            using var fillPath = CreateRoundedPath(Rectangle.Round(fill), height / 2);
            using var fillBrush = new SolidBrush(PrimaryColor);
            e.Graphics.FillPath(fillBrush, fillPath);
        }
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed record RunningApplication(string Name, string ExecutablePath)
    {
        public override string ToString() => Name;
    }
}
