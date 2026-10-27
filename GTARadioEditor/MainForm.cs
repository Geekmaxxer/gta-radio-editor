using System.ComponentModel;
using GTARadioEditor.Models;
using GTARadioEditor.Services;

namespace GTARadioEditor;

public sealed class MainForm : Form
{
    private const int SlotPaneMinimumWidth = 440;
    private const int MusicPaneMinimumWidth = 380;
    private readonly GtaDirectoryService _gtaDirectoryService = new();
    private readonly RpfRadioService _rpfService = new();
    private readonly BindingList<RadioSlot> _slots = [];
    private readonly BindingList<AudioTrack> _tracks = [];
    private readonly BindingList<RadioStation> _stations = [];
    private readonly List<string> _musicFolders = [];
    private readonly ComboBox _languageSelector = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 155 };
    private readonly Dictionary<Control, string> _textKeys = new();
    private readonly Dictionary<DataGridViewColumn, string> _headerKeys = new();
    private readonly Button _randomFillButton = new() { Text = L.T("Random Auto-fill"), AutoSize = true, Tag = "random" };
    private int _skippedMusicFiles;
    private bool _changingLanguage;
    private readonly TextBox _gtaDirectoryPath = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
#if !NET48
        PlaceholderText = L.T("Choose the GTA V game or port folder")
#endif
    };
    private readonly ComboBox _stationSelector = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        DisplayMember = nameof(RadioStation.DisplayName),
        Enabled = false
    };
    private readonly TextBox _musicPath = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
#if !NET48
        PlaceholderText = L.T("Add music folders: MP3, WAV, FLAC, AAC, M4A, WMA, or OGG")
#endif
    };
    private readonly TextBox _timingPath = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Button _browseTimingButton = new() { Text = L.T("Browse..."), AutoSize = true };
    private readonly DataGridView _slotGrid = new();
    private readonly ListBox _musicList = new()
    {
        Dock = DockStyle.Fill,
        DisplayMember = nameof(AudioTrack.DisplayName),
        HorizontalScrollbar = true,
        IntegralHeight = false
    };
    private readonly SplitContainer _workspace = new()
    {
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 12, 0, 8)
    };
    private readonly ToolTip _musicToolTip = new();
    private readonly Label _slotCount = new() { AutoSize = true, Text = L.T("No RPF scanned") };
    private readonly Label _musicCount = new() { AutoSize = true, Text = L.T("No music folders loaded") };
    private readonly TextBox _log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = SystemColors.Window };
    private readonly ToolStripStatusLabel _status = new() { Text = L.T("Choose a GTA V folder to find its radio stations.") };
    private readonly ToolStripStatusLabel _versionStatus = new() { Text = $"v{AppVersion.Current}", ForeColor = SystemColors.GrayText };
    private readonly Button _scanButton = new() { Text = L.T("Open selected station"), AutoSize = true, Enabled = false };
    private readonly Button _buildButton = new() { Text = L.T("Build output RPF"), AutoSize = true, Enabled = false };
    private readonly Button _assignButton = new() { Text = L.T("Assign selected"), AutoSize = true, Enabled = false };
    private readonly ProgressBar _buildProgress = new()
    {
        Dock = DockStyle.Fill,
        Minimum = 0,
        Maximum = 100,
        Style = ProgressBarStyle.Continuous,
        Visible = false
    };
    private readonly TextBox _layeredFsPath = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
#if !NET48
        PlaceholderText = "LayeredFS zip path prints here after Build output RPF",
#endif
        BackColor = SystemColors.ControlLight
    };
    private readonly Button _zipButton = new() { Text = "Zip LayeredFS", AutoSize = true, Enabled = false };
    private string? _lastBuiltRpfPath;
    private readonly List<Control> _busyControls = [];
    private string? _selectedRpfPath;
    private CancellationTokenSource? _operationCancellation;

    public MainForm()
    {
        BindingContext = new BindingContext();
        Text = L.T("GTA Radio Editor v{0} - Stable", AppVersion.Current);
        MinimumSize = new Size(1250, 740);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        BuildInterface();
#if NET48
        PlatformCompatibility.SetCueBanner(_gtaDirectoryPath, L.T("Choose the GTA V game or port folder"));
        PlatformCompatibility.SetCueBanner(_musicPath, L.T("Add music folders: MP3, WAV, FLAC, AAC, M4A, WMA, or OGG"));
#endif
        ConfigureInteractions();
        CaptureTextKeys(this);
        foreach (DataGridViewColumn column in _slotGrid.Columns)
            if (L.KeyForText(column.HeaderText) is string key) _headerKeys.Add(column, key);
        L.LoadPreference();
        ApplyLanguage();
        L.Changed += ApplyLanguage;
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 315));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var setup = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        setup.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        setup.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        setup.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var languagePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        languagePanel.Controls.Add(new Label { Text = L.T("Language"), AutoSize = true, Margin = new Padding(3, 7, 10, 0) });
        _languageSelector.DataSource = L.Languages.ToList();
        _languageSelector.SelectedIndexChanged += (_, _) =>
        {
            if (_languageSelector.SelectedItem is L.Language language && language.Code != L.Code)
                L.SetLanguage(language.Code);
        };
        languagePanel.Controls.Add(_languageSelector);
        setup.Controls.Add(languagePanel, 0, 0);

        var browseGameButton = new Button { Text = L.T("Browse..."), AutoSize = true, Tag = "gta-directory" };
        var rescanStationsButton = new Button { Text = L.T("Rescan"), AutoSize = true, Tag = "rescan-stations" };
        var gameAndStationStep = new GroupBox { Text = L.T("1. Select the GTA V folder and radio station"), Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
        var gameAndStationLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
        gameAndStationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        gameAndStationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        gameAndStationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        gameAndStationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        gameAndStationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        gameAndStationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        gameAndStationLayout.Controls.Add(new Label { Text = L.T("GTA V folder"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 0, 8, 0) }, 0, 0);
        gameAndStationLayout.Controls.Add(_gtaDirectoryPath, 1, 0);
        gameAndStationLayout.Controls.Add(browseGameButton, 2, 0);
        gameAndStationLayout.Controls.Add(new Label { Text = L.T("Radio station"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 0, 8, 0) }, 0, 1);
        gameAndStationLayout.Controls.Add(_stationSelector, 1, 1);
        gameAndStationLayout.Controls.Add(new Label { Text = L.T("Radio timing file"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        gameAndStationLayout.Controls.Add(_timingPath, 1, 2);
        gameAndStationLayout.Controls.Add(_browseTimingButton, 2, 2);
        _musicToolTip.SetToolTip(_timingPath, L.T("Base stations normally use audio_rel.rpf. For DLC stations, choose the RPF containing that station's sounds.dat54.rel, or the extracted REL file."));
        _busyControls.Add(_browseTimingButton);
        _browseTimingButton.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = L.T("Select the timing file for this radio station"),
                Filter = L.T("Radio timing files (*.rpf;*.dat54.rel)|*.rpf;*.dat54.rel"),
                FileName = _timingPath.Text
            };
            if (dialog.ShowDialog(this) == DialogResult.OK) _timingPath.Text = dialog.FileName;
        };
        var stationActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        stationActions.Controls.Add(_scanButton);
        stationActions.Controls.Add(rescanStationsButton);
        gameAndStationLayout.Controls.Add(stationActions, 2, 1);
        gameAndStationStep.Controls.Add(gameAndStationLayout);
        setup.Controls.Add(gameAndStationStep, 0, 1);

        var workflowSteps = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        workflowSteps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        workflowSteps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        workflowSteps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        var addMusicButton = new Button { Text = L.T("Add folders..."), AutoSize = true, Tag = "music" };
        var clearMusicButton = new Button { Text = L.T("Clear"), AutoSize = true, Tag = "clear-music" };
        var musicStep = new GroupBox { Text = L.T("2. Add replacement music"), Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
        var musicStepLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        musicStepLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        musicStepLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        musicStepLayout.Controls.Add(_musicPath, 0, 0);
        var musicActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        musicActions.Controls.Add(addMusicButton);
        musicActions.Controls.Add(clearMusicButton);
        musicStepLayout.Controls.Add(musicActions, 1, 0);
        musicStep.Controls.Add(musicStepLayout);
        workflowSteps.Controls.Add(musicStep, 0, 0);

        var autoFillButton = new Button { Text = L.T("Auto-fill in order"), AutoSize = true, Tag = "auto" };
        var clearAssignmentsButton = new Button { Text = L.T("Clear All Replaced"), AutoSize = true, Tag = "clear" };
        var mappingStep = new GroupBox { Text = L.T("3. Map tracks to slots"), Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
        var mappingActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, AutoScroll = true };
        mappingActions.Controls.Add(_assignButton);
        mappingActions.Controls.Add(autoFillButton);
        mappingActions.Controls.Add(_randomFillButton);
        mappingActions.Controls.Add(clearAssignmentsButton);
        mappingStep.Controls.Add(mappingActions);
        workflowSteps.Controls.Add(mappingStep, 1, 0);

        var buildStep = new GroupBox { Text = L.T("4. Build"), Dock = DockStyle.Fill };
        var buildActions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        buildActions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        buildActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        buildActions.Controls.Add(_buildButton, 0, 0);
        buildActions.Controls.Add(_buildProgress, 0, 1);
        buildStep.Controls.Add(buildActions);
        workflowSteps.Controls.Add(buildStep, 2, 0);

<<<<<<< HEAD
        setup.Controls.Add(workflowSteps, 0, 1);
        var layeredRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
        layeredRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layeredRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _zipButton.Click += (_, _) => ZipLayeredFs();
        layeredRow.Controls.Add(_zipButton, 0, 0);
        layeredRow.Controls.Add(_layeredFsPath, 1, 0);
        setup.Controls.Add(layeredRow, 0, 2);
=======
        setup.Controls.Add(workflowSteps, 0, 2);
>>>>>>> 56b39beea1c5e97761e9930455eb0e15fab21b83
        _stationSelector.DataSource = _stations;
        root.Controls.Add(setup, 0, 0);

        root.Controls.Add(_workspace, 0, 1);

        var slotPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        slotPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        slotPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        slotPanel.Controls.Add(_slotCount, 0, 0);
        ConfigureSlotGrid();
        slotPanel.Controls.Add(_slotGrid, 0, 1);
        _workspace.Panel1.Controls.Add(slotPanel);

        var musicPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12, 0, 0, 0) };
        musicPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        musicPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        musicPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        musicPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _musicList.DataSource = _tracks;
        musicPanel.Controls.Add(_musicCount, 0, 0);
        musicPanel.Controls.Add(_musicList, 0, 1);
        musicPanel.Controls.Add(new Label
        {
            Text = L.T("Use Add folders to Ctrl/Shift-select several artist folders at once. Drag a track onto a radio row, or select both and choose Assign selected.\nThe app converts MP3, WAV, FLAC, AAC, M4A, WMA, and OGG to 48 kHz 16-bit PCM while building."),
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        }, 0, 2);
        _workspace.Panel2.Controls.Add(musicPanel);

        var logPanel = new GroupBox { Text = L.T("Build log"), Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
        logPanel.Controls.Add(_log);
        root.Controls.Add(logPanel, 0, 2);

        var statusStrip = new StatusStrip();
        _status.Spring = true;
        _versionStatus.Font = new Font(Font.FontFamily, Math.Max(Font.Size - 1f, 7f));
        statusStrip.Items.Add(_status);
        statusStrip.Items.Add(_versionStatus);
        root.Controls.Add(statusStrip, 0, 3);

        foreach (var button in new[] { browseGameButton, rescanStationsButton, addMusicButton, clearMusicButton, autoFillButton, _randomFillButton, clearAssignmentsButton })
        {
            button.Click += ButtonClick;
        }

        _busyControls.AddRange([
            browseGameButton,
            rescanStationsButton,
            addMusicButton,
            clearMusicButton,
            autoFillButton,
            _randomFillButton,
            _languageSelector,
            clearAssignmentsButton,
            _gtaDirectoryPath,
            _stationSelector,
            _scanButton,
            _buildButton,
            _assignButton,
            _slotGrid,
            _musicList
        ]);
    }

    private void ConfigureSlotGrid()
    {
        _slotGrid.Dock = DockStyle.Fill;
        _slotGrid.AutoGenerateColumns = false;
        _slotGrid.AllowUserToAddRows = false;
        _slotGrid.AllowUserToDeleteRows = false;
        _slotGrid.AllowDrop = true;
        _slotGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _slotGrid.MultiSelect = false;
        _slotGrid.ReadOnly = true;
        _slotGrid.RowHeadersVisible = false;
        _slotGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _slotGrid.DataSource = _slots;
        _slotGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = L.T("GTA radio slot"), DataPropertyName = nameof(RadioSlot.ContainerName), FillWeight = 26 });
        _slotGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = L.T("Original"), DataPropertyName = nameof(RadioSlot.OriginalDuration), FillWeight = 12 });
        _slotGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = L.T("Channels"), DataPropertyName = nameof(RadioSlot.LeftChannelName), FillWeight = 23 });
        _slotGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = L.T("Replacement"), DataPropertyName = nameof(RadioSlot.ReplacementDisplay), FillWeight = 31 });
        _slotGrid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "ClearReplacement",
            HeaderText = string.Empty,
            Text = L.T("Clear"),
            UseColumnTextForButtonValue = true,
            FillWeight = 8
        });
    }

    private void ConfigureInteractions()
    {
        _musicList.MouseDown += (_, args) =>
        {
            if (_musicList.IndexFromPoint(args.Location) >= 0 && _musicList.SelectedItem is AudioTrack track)
            {
                _musicList.DoDragDrop(track.FilePath, DragDropEffects.Copy);
            }
        };
        _musicList.MouseMove += (_, args) =>
        {
            var index = _musicList.IndexFromPoint(args.Location);
            _musicToolTip.SetToolTip(_musicList,
                index >= 0 && index < _tracks.Count ? _tracks[index].FilePath : string.Empty);
        };
        _slotGrid.DragEnter += (_, args) =>
        {
            args.Effect = TryExtractAudioPath(args.Data, out var _) ? DragDropEffects.Copy : DragDropEffects.None;
        };
        _slotGrid.DragDrop += (_, args) =>
        {
            if (!TryExtractAudioPath(args.Data, out var path) || path is null)
            {
                return;
            }
            var point = _slotGrid.PointToClient(new Point(args.X, args.Y));
            var hit = _slotGrid.HitTest(point.X, point.Y);
            if (hit.RowIndex >= 0 && hit.RowIndex < _slots.Count)
            {
                Assign(_slots[hit.RowIndex], path);
            }
        };
        _slotGrid.DoubleClick += (_, _) => AssignSelected();
        _slotGrid.CellContentClick += (_, args) =>
        {
            if (args.RowIndex < 0 || _slotGrid.Columns[args.ColumnIndex].Name != "ClearReplacement")
            {
                return;
            }

            if (_slotGrid.Rows[args.RowIndex].DataBoundItem is RadioSlot slot && slot.ReplacementPath is not null)
            {
                slot.ReplacementPath = null;
                RefreshSlots();
                _status.Text = L.T("Cleared {0}.", slot.ContainerName);
            }
        };
        _stationSelector.SelectedIndexChanged += (_, _) => { if (!_changingLanguage) SelectStation(); };
        FormClosing += (_, _) => _operationCancellation?.Cancel();
    }

    private async void ButtonClick(object? sender, EventArgs eventArgs)
    {
        if (sender is not Button { Tag: string action })
        {
            return;
        }
        switch (action)
        {
            case "gta-directory":
                using (var dialog = new FolderBrowserDialog { Description = L.T("Select the GTA V game or port folder") })
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK) await ScanGtaDirectoryAsync(dialog.SelectedPath);
                }
                break;
            case "rescan-stations":
                if (Directory.Exists(_gtaDirectoryPath.Text)) await ScanGtaDirectoryAsync(_gtaDirectoryPath.Text);
                break;
            case "music":
                await AddMusicFoldersAsync();
                break;
            case "auto":
                AutoAssign();
                break;
            case "random":
                AutoAssign(randomize: true);
                break;
            case "clear":
                TrackAssignmentService.ClearAll(_slots);
                RefreshSlots();
                _status.Text = L.T("All replacements cleared.");
                break;
            case "clear-music":
                ClearMusicFolders();
                break;
        }
    }

    protected override void OnShown(EventArgs eventArgs)
    {
        base.OnShown(eventArgs);
        BeginInvoke((MethodInvoker)SetInitialWorkspaceSplit);
        _scanButton.Click += async (_, _) => await ScanRpfAsync();
        _assignButton.Click += (_, _) => AssignSelected();
        _buildButton.Click += async (_, _) => await BuildOutputAsync();
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        var result = await UpdateChecker.CheckAsync();

        if (IsDisposed || !IsHandleCreated)
            return;

        if (result is not { UpdateAvailable: true })
            return;

        using var dialog = new UpdateAvailableDialog(result.CurrentVersion, result.LatestVersion, result.ReleaseUrl);
        dialog.ShowDialog(this);
    }

    private async Task ScanGtaDirectoryAsync(string gtaDirectory)
    {
        _gtaDirectoryPath.Text = gtaDirectory;
        _musicToolTip.SetToolTip(_gtaDirectoryPath, gtaDirectory);
        _stationSelector.Enabled = false;
        _stations.Clear();
        SelectStation();

        await RunOperationAsync(async (progress, cancellationToken) =>
        {
            var stations = await _gtaDirectoryService.DiscoverRadioStationsAsync(gtaDirectory, progress, cancellationToken);
            foreach (var station in stations)
            {
                _stations.Add(station);
            }

            _stationSelector.SelectedIndex = -1;
            _stationSelector.Enabled = _stations.Count > 0;
            _slotCount.Text = L.T("{0} radio station archive(s) found. Select one to continue.", _stations.Count);
            _status.Text = L.T("Select a radio station to inspect its music slots.");
        });
    }

    private void SelectStation()
    {
        var station = _stationSelector.SelectedItem as RadioStation;
        _timingPath.Text = station is null ? string.Empty : RadioTimingService.FindDefaultTimingPath(station.RpfPath) ?? string.Empty;
        _selectedRpfPath = station?.RpfPath;
        _musicToolTip.SetToolTip(_stationSelector, station?.FullDisplayName ?? string.Empty);
        _scanButton.Enabled = station?.IsAvailable == true;
        _buildButton.Enabled = false;
        _slots.Clear();
        _slotGrid.Refresh();

        if (station is null)
        {
            _slotCount.Text = _stations.Count == 0 ? L.T("No RPF scanned") : L.T("Select a radio station to inspect");
            return;
        }

        if (!station.IsAvailable)
        {
            _slotCount.Text = L.T("{0} is unavailable", station.StationName);
            _status.Text = L.T(station.DisabledReason!);
            return;
        }

        _slotCount.Text = L.T("{0} selected. Open it to inspect its music slots.", station.StationName);
        _status.Text = L.T("Selected {0}.", station.DisplayName);
    }

    private async Task ScanRpfAsync()
    {
        var rpfPath = _selectedRpfPath;
        var station = _stationSelector.SelectedItem as RadioStation;
        if (string.IsNullOrWhiteSpace(rpfPath) || !File.Exists(rpfPath) || station?.IsAvailable != true) return;
        await RunOperationAsync(async (progress, cancellationToken) =>
        {
            var slots = await _rpfService.ScanMusicSlotsAsync(rpfPath!, progress, cancellationToken);
            _slots.Clear();

            if (slots.Count == 0)
            {
                DisableStation(station, "No compatible stereo music slots were found in this archive.");
                return;
            }

            foreach (var slot in slots) _slots.Add(slot);
            _slotCount.Text = L.T("{0} replaceable music slot(s) detected", slots.Count);
            _buildButton.Enabled = slots.Count > 0;
            RefreshSlots();
        });
    }

    private void DisableStation(RadioStation station, string reason)
    {
        var stationIndex = _stations.IndexOf(station);
        if (stationIndex < 0)
        {
            return;
        }

        _stations[stationIndex] = station with { DisabledReason = reason };
        _stationSelector.SelectedIndex = stationIndex;
        SelectStation();
        AppendLog(L.T("WARNING: {0} is unavailable. {1}", station.StationName, L.T(reason)));
    }

    private async Task AddMusicFoldersAsync()
    {
        var selectedFolders = MultiFolderPicker.Pick(this, L.T("Select one or more music folders (Ctrl/Shift to select several)"));
        var foldersAdded = selectedFolders
            .Where(Directory.Exists)
            .Where(path => !_musicFolders.Contains(path, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (foldersAdded.Count == 0)
        {
            return;
        }

        foreach (var folder in foldersAdded)
        {
            _musicFolders.Add(folder);
        }

        UpdateMusicFolderDisplay();
        await LoadMusicFoldersAsync();
    }

    private async Task LoadMusicFoldersAsync()
    {
        var folders = _musicFolders.Where(Directory.Exists).ToArray();
        if (folders.Length == 0)
        {
            return;
        }

        var skippedFiles = new List<string>();
        await RunOperationAsync(async (progress, cancellationToken) =>
        {
            _tracks.Clear();
            progress.Report(L.T("Reading music from {0} folder(s)...", folders.Length));
            var files = folders
                .SelectMany(folder => Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly))
                .Where(AudioConversionService.IsSupportedFile)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
                .ThenBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    _tracks.Add(await AudioConversionService.InspectAsync(file, cancellationToken));
                }
                catch (Exception exception)
                {
                    skippedFiles.Add($"{Path.GetFileName(file)}: {exception.Message}");
                    progress.Report(L.T("Skipped {0}: {1}", Path.GetFileName(file), exception.Message));
                }
            }
            _musicCount.Text = L.T("{0} usable audio file(s) from {1} folder(s)", _tracks.Count, folders.Length) +
                (skippedFiles.Count == 0 ? string.Empty : L.T(" ({0} skipped)", skippedFiles.Count));
            _skippedMusicFiles = skippedFiles.Count;
            UpdateMusicListHorizontalExtent();
            _assignButton.Enabled = _tracks.Count > 0 && _slots.Count > 0;
            _randomFillButton.Enabled = _tracks.Count > 0 && _slots.Count > 0;
        });

        if (skippedFiles.Count > 0)
        {
            var details = string.Join(Environment.NewLine, skippedFiles.Take(8).Select(item => $"• {item}"));
            var remaining = skippedFiles.Count - Math.Min(skippedFiles.Count, 8);
            if (remaining > 0) details += L.T("{0}• …and {1} more.", Environment.NewLine, remaining);
            MessageBox.Show(this,
                L.T("{0} file(s) could not be decoded and were not added.{1}{2}{3}{4}{5}See the build log for the full list.", skippedFiles.Count, Environment.NewLine, Environment.NewLine, details, Environment.NewLine, Environment.NewLine),
                L.T("Music warnings"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ClearMusicFolders()
    {
        _musicFolders.Clear();
        _tracks.Clear();
        _skippedMusicFiles = 0;
        _musicPath.Clear();
        _musicToolTip.SetToolTip(_musicPath, string.Empty);
        _musicCount.Text = L.T("No music folders loaded");
        _musicList.HorizontalExtent = 0;
        _assignButton.Enabled = false;
    }

    private void UpdateMusicFolderDisplay()
    {
        _musicPath.Text = _musicFolders.Count == 1
            ? _musicFolders[0]
            : L.T("{0} music folders selected", _musicFolders.Count);
        _musicToolTip.SetToolTip(_musicPath, string.Join(Environment.NewLine, _musicFolders));
    }

    private void SetInitialWorkspaceSplit()
    {
        var availableWidth = _workspace.ClientSize.Width - _workspace.SplitterWidth;
        if (availableWidth < SlotPaneMinimumWidth + MusicPaneMinimumWidth)
        {
            return;
        }

        var preferredLeftWidth = (int)Math.Round(availableWidth * 0.60);
        _workspace.SplitterDistance = PlatformCompatibility.Clamp(preferredLeftWidth, SlotPaneMinimumWidth, availableWidth - MusicPaneMinimumWidth);
        _workspace.Panel1MinSize = SlotPaneMinimumWidth;
        _workspace.Panel2MinSize = MusicPaneMinimumWidth;
    }

    private void UpdateMusicListHorizontalExtent()
    {
        var widestTrack = _tracks.Count == 0
            ? 0
            : _tracks.Max(track => TextRenderer.MeasureText(track.DisplayName, _musicList.Font).Width);
        _musicList.HorizontalExtent = widestTrack + SystemInformation.VerticalScrollBarWidth + 12;
    }

    private void AssignSelected()
    {
        if (_slotGrid.CurrentRow?.DataBoundItem is RadioSlot slot && _musicList.SelectedItem is AudioTrack track)
        {
            Assign(slot, track.FilePath);
        }
    }

    private void Assign(RadioSlot slot, string path)
    {
        if (!File.Exists(path) || !AudioConversionService.IsSupportedFile(path))
        {
            MessageBox.Show(this, L.T("Select MP3, WAV, FLAC, AAC, M4A, WMA, or OGG audio."), L.T("Unsupported audio"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        slot.ReplacementPath = path;
        RefreshSlots();
    }

    private void AutoAssign(bool randomize = false)
    {
        var count = TrackAssignmentService.Fill(_slots, _tracks.ToList(), randomize);
        RefreshSlots();
        _status.Text = randomize ? L.T("Randomly assigned {0} track(s).", count) : L.T("Assigned {0} track(s) in order.", count);
    }

    private async Task BuildOutputAsync()
    {
        var rpfPath = _selectedRpfPath;
        if (string.IsNullOrWhiteSpace(rpfPath) || !File.Exists(rpfPath))
        {
            MessageBox.Show(this, L.T("Select and open a radio station before building."), L.T("No radio station selected"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_slots.All(slot => slot.ReplacementPath is null))
        {
            MessageBox.Show(this, L.T("Assign at least one track before building."), L.T("Nothing to build"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Filter = L.T("GTA RPF archive (*.rpf)|*.rpf"),
            FileName = Path.GetFileName(rpfPath),
            InitialDirectory = Path.Combine(Path.GetDirectoryName(rpfPath)!, "GTARadioEditor Output"),
            Title = L.T("Save rebuilt radio RPF")
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!ConfirmBuild(dialog.FileName)) return;

        _buildProgress.Value = 0;
        _buildProgress.Visible = true;
        try
        {
            await RunOperationAsync(async (progress, cancellationToken) =>
            {
                var buildProgress = new Progress<int>(value => _buildProgress.Value = PlatformCompatibility.Clamp(value, 0, 100));
<<<<<<< HEAD
                var result = await _rpfService.BuildOutputAsync(rpfPath!, dialog.FileName, _slots, progress, buildProgress, cancellationToken);
                AppendLog($"\r\nSuccess: {result.ReplacedContainers} container(s) rebuilt.\r\nOutput: {result.OutputRpfPath}");
                _lastBuiltRpfPath = result.OutputRpfPath;
                _zipButton.Enabled = true;
                _layeredFsPath.Text = string.Empty;
                AppendLog("SD option: press Zip LayeredFS for atmosphere/contents/0100B00B51230000/romfs/switch/audio/sfx/<station>.rpf.");
=======
                var result = await _rpfService.BuildOutputAsync(rpfPath!, dialog.FileName, _slots, _timingPath.Text, progress, buildProgress, cancellationToken);
                AppendLog(L.T("\r\nSuccess: {0} container(s) rebuilt.\r\nOutput: {1}", result.ReplacedContainers, result.OutputRpfPath));
                foreach (var message in result.Messages) AppendLog(message);
                var sourceRadioRelative = PlatformCompatibility.GetRelativePath(_gtaDirectoryPath.Text, rpfPath!);
                var timingInstallPath = DescribeTimingInstallPath(rpfPath!, result.SourceTimingPath);
>>>>>>> 56b39beea1c5e97761e9930455eb0e15fab21b83
                MessageBox.Show(this,
                    L.T("Built {0} replacement(s) with matching song durations and retimed cues.\n\nInstall both outputs at their original game paths (or the equivalent mods paths):\n\n{1}\n → {2}\n\n{3}\n → {4}\n\nKeep both file names unchanged. The timing file is required for correct playback.", result.ReplacedContainers, result.OutputRpfPath, sourceRadioRelative, result.OutputTimingPath, timingInstallPath),
                    L.T("Build complete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }
        finally
        {
            _buildProgress.Visible = false;
            _buildProgress.Value = 0;
        }
    }

    private string DescribeTimingInstallPath(string radioPath, string timingPath)
    {
        if (timingPath.EndsWith(".dat54.rel", StringComparison.OrdinalIgnoreCase))
            return L.T("Import {0} into its original entry in the game's timing archive.", Path.GetFileName(timingPath));
        var gameRoot = Path.GetFullPath(_gtaDirectoryPath.Text).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (Path.GetFullPath(timingPath).StartsWith(gameRoot, StringComparison.OrdinalIgnoreCase))
            return PlatformCompatibility.GetRelativePath(gameRoot, timingPath);
        var originalTimingPath = RadioTimingService.FindDefaultTimingPath(radioPath);
        if (originalTimingPath is not null && Path.GetFileName(originalTimingPath).Equals(Path.GetFileName(timingPath), StringComparison.OrdinalIgnoreCase))
            return PlatformCompatibility.GetRelativePath(gameRoot, originalTimingPath);
        return L.T("Replace this station's original timing archive ({0}) at its original game path.", Path.GetFileName(timingPath));
    }

    private bool ConfirmBuild(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(_timingPath.Text) || !File.Exists(_timingPath.Text))
        {
            MessageBox.Show(this, L.T("Select the timing file for this station. Base stations normally use audio_rel.rpf; DLC stations need the archive containing their sounds.dat54.rel."),
                L.T("Radio timing required"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        var station = _stationSelector.SelectedItem as RadioStation;
        var assigned = _slots.Where(slot => !string.IsNullOrWhiteSpace(slot.ReplacementPath)).ToList();
        var unavailableSources = assigned
            .Where(slot => !File.Exists(slot.ReplacementPath!) || !AudioConversionService.IsSupportedFile(slot.ReplacementPath!))
            .Select(slot => Path.GetFileName(slot.ReplacementPath!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unavailableSources.Count > 0)
        {
            MessageBox.Show(this,
                L.T("These assigned files are missing or no longer supported:{0}{1}", Environment.NewLine, Environment.NewLine) +
                string.Join(Environment.NewLine, unavailableSources.Select(file => $"- {file}")),
                L.T("Build blocked"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var warnings = new List<string>();
        var timingOutput = Path.Combine(Path.GetDirectoryName(outputPath)!, Path.GetFileName(_timingPath.Text));
        if (File.Exists(timingOutput)) warnings.Add(L.T("The existing timing output will be replaced: {0}", timingOutput));
        var unassignedCount = _slots.Count - assigned.Count;
        if (unassignedCount > 0)
        {
            warnings.Add(L.T("{0} slot(s) are unassigned and will keep their original GTA audio.", unassignedCount));
        }

        var duplicateAssignments = assigned
            .GroupBy(slot => slot.ReplacementPath!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => L.T("{0} is assigned to {1} slots.", Path.GetFileName(group.Key), group.Count()))
            .ToList();
        warnings.AddRange(duplicateAssignments);

        var review = new List<string>
        {
            L.T("Ready to build:"),
            string.Empty,
            L.T("Station: {0}", station?.StationName ?? Path.GetFileNameWithoutExtension(_selectedRpfPath)),
            L.T("Replacements: {0}/{1}", assigned.Count, _slots.Count),
            L.T("Radio output: {0}", outputPath),
            L.T("Timing output: {0}", timingOutput),
            L.T("Song durations and cue positions will follow the converted audio. Install both outputs.")
        };
        if (warnings.Count > 0)
        {
            review.Add(string.Empty);
            review.Add(L.T("Warnings:"));
            review.AddRange(warnings.Select(warning => $"- {warning}"));
        }
        review.Add(string.Empty);
        review.Add(L.T("Build this RPF now?"));

        return MessageBox.Show(this, string.Join(Environment.NewLine, review), L.T("Confirm build"),
            MessageBoxButtons.YesNo, warnings.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private async Task RunOperationAsync(Func<IProgress<string>, CancellationToken, Task> operation)
    {
        if (_operationCancellation is not null) return;
        _operationCancellation = new CancellationTokenSource();
        SetBusy(true);
        var progress = new Progress<string>(message =>
        {
            _status.Text = message;
            AppendLog(message);
        });
        try
        {
            await operation(progress, _operationCancellation.Token);
            _status.Text = L.T("Ready");
        }
        catch (OperationCanceledException)
        {
            _status.Text = L.T("Cancelled");
            AppendLog(L.T("Operation cancelled."));
        }
        catch (Exception exception)
        {
            _status.Text = L.T("Failed");
            AppendLog(L.T("ERROR: ") + exception.Message);
            MessageBox.Show(this, exception.Message, L.T("GTA Radio Editor"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        foreach (var control in _busyControls) control.Enabled = !busy;
        if (!busy)
        {
            var station = _stationSelector.SelectedItem as RadioStation;
            _stationSelector.Enabled = _stations.Count > 0;
            _scanButton.Enabled = station?.IsAvailable == true;
            _buildButton.Enabled = station?.IsAvailable == true && _slots.Count > 0;
            _assignButton.Enabled = _tracks.Count > 0 && _slots.Count > 0;
<<<<<<< HEAD
            _zipButton.Enabled = _lastBuiltRpfPath is not null && File.Exists(_lastBuiltRpfPath);
        }
    }

    private void ZipLayeredFs()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_lastBuiltRpfPath) || !File.Exists(_lastBuiltRpfPath))
            {
                MessageBox.Show(this, "Build an output RPF first, then zip it for Switch LayeredFS.", "GTA Radio Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var zipPath = SwitchZipService.BuildLayeredFsZip(_lastBuiltRpfPath!, null);
            _layeredFsPath.Text = zipPath;
            AppendLog("LayeredFS zip: " + zipPath);
            AppendLog("SD target: " + SwitchZipService.LayeredFsHint(Path.GetFileName(_lastBuiltRpfPath)));
            AppendLog("Run the GTAVPatcher Switch conversion step before copying to the SD card.");
        }
        catch (Exception exception)
        {
            AppendLog("ZIP ERROR: " + exception.Message);
            MessageBox.Show(this, exception.Message, "GTA Radio Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
=======
            _randomFillButton.Enabled = _tracks.Count > 0 && _slots.Count > 0;
>>>>>>> 56b39beea1c5e97761e9930455eb0e15fab21b83
        }
    }

    private void RefreshSlots()
    {
        _slotGrid.Refresh();
        _assignButton.Enabled = _tracks.Count > 0 && _slots.Count > 0;
        _randomFillButton.Enabled = _tracks.Count > 0 && _slots.Count > 0;
    }

    private void CaptureTextKeys(Control control)
    {
        if (L.KeyForText(control.Text) is string key) _textKeys[control] = key;
        foreach (Control child in control.Controls) CaptureTextKeys(child);
    }

    private void ApplyLanguage()
    {
        if (_changingLanguage) return;
        _changingLanguage = true;
        var selectedStation = _stationSelector.SelectedItem;
        SuspendLayout();
        try
        {
            Text = L.T("GTA Radio Editor v{0} - Stable", AppVersion.Current);
            foreach (var entry in _textKeys) entry.Key.Text = L.T(entry.Value);
            foreach (var entry in _headerKeys) entry.Key.HeaderText = L.T(entry.Value);
            if (_slotGrid.Columns["ClearReplacement"] is DataGridViewButtonColumn clearColumn) clearColumn.Text = L.T("Clear");
            _languageSelector.SelectedItem = L.Languages.First(l => l.Code == L.Code);
            var station = _stationSelector.SelectedItem as RadioStation;
            _stations.ResetBindings();
            _stationSelector.SelectedItem = selectedStation;
            _slotGrid.Refresh();
            _musicToolTip.SetToolTip(_stationSelector, station?.FullDisplayName ?? string.Empty);
            _slotCount.Text = _slots.Count > 0 ? L.T("{0} replaceable music slot(s) detected", _slots.Count) :
                station is null ? L.T("No RPF scanned") :
                station.IsAvailable ? L.T("{0} selected. Open it to inspect its music slots.", station.StationName) : L.T("{0} is unavailable", station.StationName);
            _musicCount.Text = _musicFolders.Count == 0 ? L.T("No music folders loaded") :
                L.T("{0} usable audio file(s) from {1} folder(s)", _tracks.Count, _musicFolders.Count) +
                (_skippedMusicFiles == 0 ? string.Empty : L.T(" ({0} skipped)", _skippedMusicFiles));
            if (_musicFolders.Count > 0) UpdateMusicFolderDisplay();
            _status.Text = station is { IsAvailable: false } ? L.T(station.DisabledReason!) :
                _selectedRpfPath is null ? L.T("Choose a GTA V folder to find its radio stations.") : L.T("Ready");
            _musicToolTip.SetToolTip(_timingPath, L.T("Base stations normally use audio_rel.rpf. For DLC stations, choose the RPF containing that station's sounds.dat54.rel, or the extracted REL file."));
#if NET48
            PlatformCompatibility.SetCueBanner(_gtaDirectoryPath, L.T("Choose the GTA V game or port folder"));
            PlatformCompatibility.SetCueBanner(_musicPath, L.T("Add music folders: MP3, WAV, FLAC, AAC, M4A, WMA, or OGG"));
#else
            _gtaDirectoryPath.PlaceholderText = L.T("Choose the GTA V game or port folder");
            _musicPath.PlaceholderText = L.T("Add music folders: MP3, WAV, FLAC, AAC, M4A, WMA, or OGG");
#endif
        }
        finally
        {
            ResumeLayout(performLayout: true);
            _changingLanguage = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) L.Changed -= ApplyLanguage;
        base.Dispose(disposing);
    }

    private void AppendLog(string message)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static bool TryExtractAudioPath(IDataObject? data, out string? path)
    {
        path = data?.GetData(DataFormats.UnicodeText) as string;
        if (path is not null && AudioConversionService.IsSupportedFile(path)) return true;
        if (data?.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1 && AudioConversionService.IsSupportedFile(files[0]))
        {
            path = files[0];
            return true;
        }
        path = null;
        return false;
    }

}
