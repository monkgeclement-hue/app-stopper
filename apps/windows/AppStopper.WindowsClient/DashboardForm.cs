using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AppStopper.Core;

namespace AppStopper.WindowsClient;

internal sealed class DashboardForm : Form
{
    private readonly LocalStateStore _store = new();
    private readonly List<TrackedRule> _rules;
    private readonly ComboBox _applicationPicker = new();
    private readonly NumericUpDown _allowanceMinutes = new();
    private readonly DateTimePicker _dailyResetTime = new();
    private readonly ListView _rulesView = new();
    private readonly Label _emptyRulesHint = new();
    private readonly Label _status = new();
    private readonly Label _trackedAppsValue = new();
    private readonly Label _usedTodayValue = new();
    private readonly Label _remainingValue = new();
    private readonly Button _monitorButton = new();
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
        Text = "App Stopper — Windows prototype";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 560);
        Size = new Size(1_020, 680);
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
            Text = "App Stopper",
            Visible = true,
            ContextMenuStrip = _trayMenu
        };
        _trayMenu.Items.Add("Open App Stopper", null, (_, _) => OpenSettings());
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Exit App Stopper", null, (_, _) => ExitApplication());
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
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(24),
            BackColor = Color.FromArgb(247, 249, 252)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var heading = new Label
        {
            Text = "App Stopper",
            AutoSize = true,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 40, 56),
            Margin = new Padding(0, 0, 0, 4)
        };
        var subtitle = new Label
        {
            Text = "A calmer way to manage your time across the apps you choose.",
            AutoSize = true,
            ForeColor = Color.FromArgb(87, 101, 119),
            Margin = new Padding(0, 0, 0, 18)
        };
        var headingPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        headingPanel.Controls.Add(heading);
        headingPanel.Controls.Add(subtitle);
        layout.Controls.Add(headingPanel, 0, 0);

        var summary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = true,
            BackColor = layout.BackColor,
            Margin = new Padding(0, 0, 0, 16)
        };
        for (var column = 0; column < summary.ColumnCount; column++)
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / summary.ColumnCount));
        summary.Controls.Add(CreateMetricCard("Apps with limits", _trackedAppsValue), 0, 0);
        summary.Controls.Add(CreateMetricCard("Used this cycle", _usedTodayValue), 1, 0);
        summary.Controls.Add(CreateMetricCard("Allowance left (all apps)", _remainingValue, last: true), 2, 0);
        layout.Controls.Add(summary, 0, 1);

        var setup = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(12, 10, 12, 2),
            Margin = new Padding(0)
        };
        _applicationPicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _applicationPicker.Width = 270;
        _applicationPicker.Margin = new Padding(0, 0, 8, 8);
        setup.Controls.Add(_applicationPicker);

        var refreshButton = new Button { Text = "Refresh apps", AutoSize = true };
        refreshButton.Click += (_, _) => RefreshRunningApplications();
        setup.Controls.Add(refreshButton);
        setup.Controls.Add(new Label { Text = "Daily minutes", AutoSize = true, Margin = new Padding(14, 8, 4, 0) });
        _allowanceMinutes.Minimum = 1;
        _allowanceMinutes.Maximum = 1_440;
        _allowanceMinutes.Value = 60;
        _allowanceMinutes.Width = 82;
        setup.Controls.Add(_allowanceMinutes);
        setup.Controls.Add(new Label { Text = "Reset at", AutoSize = true, Margin = new Padding(14, 8, 4, 0) });
        _dailyResetTime.Format = DateTimePickerFormat.Time;
        _dailyResetTime.ShowUpDown = true;
        _dailyResetTime.Value = DateTime.Today.AddHours(4);
        _dailyResetTime.Width = 110;
        setup.Controls.Add(_dailyResetTime);

        var addButton = new Button { Text = "Add app limit", AutoSize = true, Margin = new Padding(14, 0, 0, 8) };
        addButton.Click += (_, _) => AddRule();
        setup.Controls.Add(addButton);
        _monitorButton.AutoSize = true;
        _monitorButton.Margin = new Padding(0, 0, 12, 8);
        _monitorButton.Click += (_, _) => ToggleMonitoring();
        setup.Controls.Add(_monitorButton);
        _removeButton.Text = "Remove selected limit";
        _removeButton.AutoSize = true;
        _removeButton.Margin = new Padding(0, 0, 0, 8);
        _removeButton.Click += (_, _) => RemoveSelectedRule();
        setup.Controls.Add(_removeButton);
        var setupCard = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 14)
        };
        var setupLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            BackColor = Color.White
        };
        setupLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        setupLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        setupLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        setupLayout.Controls.Add(new Label
        {
            Text = "Create an app limit",
            AutoSize = true,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 40, 56),
            Margin = new Padding(2, 0, 0, 2)
        }, 0, 0);
        setupLayout.Controls.Add(setup, 0, 1);
        setupCard.Controls.Add(setupLayout);
        layout.Controls.Add(setupCard, 0, 2);

        _rulesView.Dock = DockStyle.Fill;
        _rulesView.View = View.Details;
        _rulesView.FullRowSelect = true;
        _rulesView.GridLines = true;
        _rulesView.MultiSelect = false;
        _rulesView.SelectedIndexChanged += (_, _) => UpdateGuardedControls();
        _rulesView.BorderStyle = BorderStyle.None;
        _rulesView.BackColor = Color.White;
        _rulesView.Columns.Add("App", 220);
        _rulesView.Columns.Add("Daily limit", 110);
        _rulesView.Columns.Add("Used", 100);
        _rulesView.Columns.Add("Status", 180);
        _rulesView.Columns.Add("Next reset", 150);
        var rulesSection = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = layout.BackColor,
            Margin = new Padding(0)
        };
        rulesSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rulesSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rulesSection.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rulesSection.Controls.Add(new Label
        {
            Text = "Your app limits",
            AutoSize = true,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 40, 56),
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);
        var rulesContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _rulesView.Dock = DockStyle.Fill;
        rulesContainer.Controls.Add(_rulesView);
        _emptyRulesHint.Text = "No limits yet\nChoose an app above to create your first daily limit.";
        _emptyRulesHint.Dock = DockStyle.Fill;
        _emptyRulesHint.TextAlign = ContentAlignment.MiddleCenter;
        _emptyRulesHint.Font = new Font("Segoe UI", 11, FontStyle.Regular);
        _emptyRulesHint.ForeColor = Color.FromArgb(87, 101, 119);
        _emptyRulesHint.BackColor = Color.White;
        rulesContainer.Controls.Add(_emptyRulesHint);
        rulesSection.Controls.Add(rulesContainer, 0, 1);
        layout.Controls.Add(rulesSection, 0, 3);

        _status.AutoSize = true;
        _status.Dock = DockStyle.Fill;
        _status.Padding = new Padding(0, 12, 0, 0);
        _status.ForeColor = Color.FromArgb(70, 83, 101);
        layout.Controls.Add(_status, 0, 4);
        Controls.Add(layout);
    }

    private static Panel CreateMetricCard(string title, Label value, bool last = false)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 88,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, last ? 0 : 12, 0)
        };
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            ForeColor = Color.FromArgb(87, 101, 119),
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            Margin = new Padding(0)
        }, 0, 0);
        value.AutoSize = true;
        value.ForeColor = Color.FromArgb(28, 40, 56);
        value.Font = new Font("Segoe UI", 20, FontStyle.Bold);
        value.Margin = new Padding(0, 2, 0, 0);
        content.Controls.Add(value, 0, 1);
        card.Controls.Add(content);
        return card;
    }

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
        _rules.Add(new TrackedRule { Rule = rule, State = new UsageCycleState() });
        SaveImmediately();
        RefreshRulesView();
        _status.Text = $"Added a local limit for {app.Name}. The app must be running for it to appear in the picker.";
    }

    private void RemoveSelectedRule()
    {
        if (_rulesView.SelectedIndices.Count == 0)
            return;
        var index = _rulesView.SelectedIndices[0];
        if (index < 0 || index >= _rules.Count)
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
        _monitorButton.Text = _monitoring ? "Pause monitoring" : "Resume monitoring";
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
        _notifyIcon.BalloonTipTitle = $"App Stopper — {appName}";
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
        foreach (var tracked in _rules)
        {
            var state = tracked.State;
            var remainingSeconds = Math.Max(0, tracked.Rule.AllowanceSeconds - state.UsedSeconds);
            var status = !tracked.Rule.Enabled ? "Off" : remainingSeconds == 0 ? "Blocked" : "Available";
            var reset = state.CycleEndUtc?.ToLocalTime().ToString("t", CultureInfo.CurrentCulture) ?? "Starting…";
            var row = new ListViewItem(tracked.Rule.Target.DisplayName);
            row.SubItems.Add(FormatDuration(tracked.Rule.AllowanceSeconds));
            row.SubItems.Add(FormatDuration(state.UsedSeconds));
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
        _emptyRulesHint.Visible = _rules.Count == 0;
        UpdateDashboardSummary();
        UpdateGuardedControls();
    }

    private void UpdateDashboardSummary()
    {
        var trackedRules = _rules.Where(item => item.Rule.Enabled).ToArray();
        var usedSeconds = trackedRules.Sum(item => (long)item.State.UsedSeconds);
        var remainingSeconds = trackedRules.Sum(item =>
            Math.Max(0, item.Rule.AllowanceSeconds - item.State.UsedSeconds));

        _trackedAppsValue.Text = trackedRules.Length.ToString(CultureInfo.CurrentCulture);
        _usedTodayValue.Text = FormatDuration((int)Math.Min(int.MaxValue, usedSeconds));
        _remainingValue.Text = FormatDuration((int)Math.Min(int.MaxValue, remainingSeconds));
    }

    private void UpdateGuardedControls()
    {
        if (_rulesView.IsDisposed || _removeButton.IsDisposed || _monitorButton.IsDisposed)
            return;

        var now = DateTimeOffset.UtcNow;
        var activeBlock = HasActiveBlock(now);
        _monitorButton.Enabled = _stateAvailable && (!_monitoring || !activeBlock);

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
            ? "Monitoring is active. Only selected-app foreground time is counted; rules and counters stay local."
            : "Monitoring is paused. App use is not being counted while paused.";
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
            _notifyIcon.BalloonTipTitle = "App Stopper is still running";
            _notifyIcon.BalloonTipText = "Monitoring continues in the system tray. Double-click the icon to open settings.";
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

    private sealed record RunningApplication(string Name, string ExecutablePath)
    {
        public override string ToString() => Name;
    }
}
