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
    private readonly TextBox _gtaDirectoryPath = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        PlaceholderText = "Choose the GTA V game or port folder"
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
        PlaceholderText = "Add music folders: MP3, WAV, FLAC, AAC, M4A, WMA, or OGG"
    };
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
    private readonly Label _slotCount = new() { AutoSize = true, Text = "No RPF scanned" };
    private readonly Label _musicCount = new() { AutoSize = true, Text = "No music folders loaded" };
    private readonly TextBox _log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = SystemColors.Window };
    private readonly ToolStripStatusLabel _status = new() { Text = "Choose a GTA V folder to find its radio stations." };
    private readonly Button _scanButton = new() { Text = "Open selected station", AutoSize = true, Enabled = false };
    private readonly Button _buildButton = new() { Text = "Build output RPF", AutoSize = true, Enabled = false };
    private readonly Button _assignButton = new() { Text = "Assign selected", AutoSize = true, Enabled = false };
    private string? _selectedRpfPath;
    private CancellationTokenSource? _operationCancellation;

    public MainForm()
    {
        Text = "GTA Radio Editor";
        MinimumSize = new Size(1080, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        BuildInterface();
        ConfigureInteractions();
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var setup = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        setup.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        setup.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var browseGameButton = new Button { Text = "Browse...", AutoSize = true, Tag = "gta-directory" };
        var rescanStationsButton = new Button { Text = "Rescan", AutoSize = true, Tag = "rescan-stations" };
        var gameAndStationStep = new GroupBox { Text = "1. Select the GTA V folder and radio station", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
        var gameAndStationLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
        gameAndStationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        gameAndStationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        gameAndStationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        gameAndStationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        gameAndStationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        gameAndStationLayout.Controls.Add(new Label { Text = "GTA V folder", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 0, 8, 0) }, 0, 0);
        gameAndStationLayout.Controls.Add(_gtaDirectoryPath, 1, 0);
        gameAndStationLayout.Controls.Add(browseGameButton, 2, 0);
        gameAndStationLayout.Controls.Add(new Label { Text = "Radio station", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 0, 8, 0) }, 0, 1);
        gameAndStationLayout.Controls.Add(_stationSelector, 1, 1);
        var stationActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        stationActions.Controls.Add(_scanButton);
        stationActions.Controls.Add(rescanStationsButton);
        gameAndStationLayout.Controls.Add(stationActions, 2, 1);
        gameAndStationStep.Controls.Add(gameAndStationLayout);
        setup.Controls.Add(gameAndStationStep, 0, 0);

        var workflowSteps = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        workflowSteps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        workflowSteps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        workflowSteps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19));

        var addMusicButton = new Button { Text = "Add folders...", AutoSize = true, Tag = "music" };
        var clearMusicButton = new Button { Text = "Clear", AutoSize = true, Tag = "clear-music" };
        var musicStep = new GroupBox { Text = "2. Add replacement music", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
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

        var autoFillButton = new Button { Text = "Auto-fill in order", AutoSize = true, Tag = "auto" };
        var clearAssignmentsButton = new Button { Text = "Clear assignments", AutoSize = true, Tag = "clear" };
        var mappingStep = new GroupBox { Text = "3. Map tracks to slots", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
        var mappingActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        mappingActions.Controls.Add(_assignButton);
        mappingActions.Controls.Add(autoFillButton);
        mappingActions.Controls.Add(clearAssignmentsButton);
        mappingStep.Controls.Add(mappingActions);
        workflowSteps.Controls.Add(mappingStep, 1, 0);

        var buildStep = new GroupBox { Text = "4. Build", Dock = DockStyle.Fill };
        var buildActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        buildActions.Controls.Add(_buildButton);
        buildStep.Controls.Add(buildActions);
        workflowSteps.Controls.Add(buildStep, 2, 0);

        setup.Controls.Add(workflowSteps, 0, 1);
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
            Text = "Use Add folders to Ctrl/Shift-select several artist folders at once. Drag a track onto a radio row, or select both and choose Assign selected.\nThe app converts MP3, WAV, FLAC, AAC, M4A, WMA, and OGG to 48 kHz 16-bit PCM while building.",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        }, 0, 2);
        _workspace.Panel2.Controls.Add(musicPanel);

        var logPanel = new GroupBox { Text = "Build log", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
        logPanel.Controls.Add(_log);
        root.Controls.Add(logPanel, 0, 2);

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);
        root.Controls.Add(statusStrip, 0, 3);

        foreach (var button in new[] { browseGameButton, rescanStationsButton, addMusicButton, clearMusicButton, autoFillButton, clearAssignmentsButton })
        {
            button.Click += ButtonClick;
        }
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
        _slotGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "GTA radio slot", DataPropertyName = nameof(RadioSlot.ContainerName), FillWeight = 26 });
        _slotGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Original", DataPropertyName = nameof(RadioSlot.OriginalDuration), FillWeight = 12 });
        _slotGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Channels", DataPropertyName = nameof(RadioSlot.LeftChannelName), FillWeight = 23 });
        _slotGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Replacement", DataPropertyName = nameof(RadioSlot.ReplacementDisplay), FillWeight = 39 });
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
        _stationSelector.SelectedIndexChanged += (_, _) => SelectStation();
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
                using (var dialog = new FolderBrowserDialog { Description = "Select the GTA V game or port folder" })
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
            case "clear":
                foreach (var slot in _slots) slot.ReplacementPath = null;
                RefreshSlots();
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
            _slotCount.Text = $"{_stations.Count} radio station archive(s) found. Select one to continue.";
            _status.Text = "Select a radio station to inspect its music slots.";
        });
    }

    private void SelectStation()
    {
        var station = _stationSelector.SelectedItem as RadioStation;
        _selectedRpfPath = station?.RpfPath;
        _musicToolTip.SetToolTip(_stationSelector, station?.FullDisplayName ?? string.Empty);
        _scanButton.Enabled = station is not null;
        _buildButton.Enabled = false;
        _slots.Clear();
        _slotGrid.Refresh();

        if (station is null)
        {
            _slotCount.Text = _stations.Count == 0 ? "No RPF scanned" : "Select a radio station to inspect";
            return;
        }

        _slotCount.Text = $"{station.StationName} selected. Open it to inspect its music slots.";
        _status.Text = $"Selected {station.DisplayName}.";
    }

    private async Task ScanRpfAsync()
    {
        var rpfPath = _selectedRpfPath;
        if (string.IsNullOrWhiteSpace(rpfPath) || !File.Exists(rpfPath)) return;
        await RunOperationAsync(async (progress, cancellationToken) =>
        {
            var slots = await _rpfService.ScanMusicSlotsAsync(rpfPath, progress, cancellationToken);
            _slots.Clear();
            foreach (var slot in slots) _slots.Add(slot);
            _slotCount.Text = $"{slots.Count} replaceable music slot(s) detected";
            _buildButton.Enabled = slots.Count > 0;
            RefreshSlots();
        });
    }

    private async Task AddMusicFoldersAsync()
    {
        var selectedFolders = MultiFolderPicker.Pick(this, "Select one or more music folders (Ctrl/Shift to select several)");
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

        await RunOperationAsync(async (progress, cancellationToken) =>
        {
            _tracks.Clear();
            progress.Report($"Reading music from {folders.Length} folder(s)...");
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
                    progress.Report($"Skipped {Path.GetFileName(file)}: {exception.Message}");
                }
            }
            _musicCount.Text = $"{_tracks.Count} usable audio file(s) from {folders.Length} folder(s)";
            UpdateMusicListHorizontalExtent();
            _assignButton.Enabled = _tracks.Count > 0 && _slots.Count > 0;
        });
    }

    private void ClearMusicFolders()
    {
        _musicFolders.Clear();
        _tracks.Clear();
        _musicPath.Clear();
        _musicToolTip.SetToolTip(_musicPath, string.Empty);
        _musicCount.Text = "No music folders loaded";
        _musicList.HorizontalExtent = 0;
        _assignButton.Enabled = false;
    }

    private void UpdateMusicFolderDisplay()
    {
        _musicPath.Text = _musicFolders.Count == 1
            ? _musicFolders[0]
            : $"{_musicFolders.Count} music folders selected";
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
        _workspace.SplitterDistance = Math.Clamp(preferredLeftWidth, SlotPaneMinimumWidth, availableWidth - MusicPaneMinimumWidth);
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
            MessageBox.Show(this, "Select MP3, WAV, FLAC, AAC, M4A, WMA, or OGG audio.", "Unsupported audio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        slot.ReplacementPath = path;
        RefreshSlots();
    }

    private void AutoAssign()
    {
        if (_slots.Count == 0 || _tracks.Count == 0) return;
        for (var index = 0; index < _slots.Count; index++)
        {
            _slots[index].ReplacementPath = index < _tracks.Count ? _tracks[index].FilePath : null;
        }
        RefreshSlots();
    }

    private async Task BuildOutputAsync()
    {
        var rpfPath = _selectedRpfPath;
        if (string.IsNullOrWhiteSpace(rpfPath) || !File.Exists(rpfPath))
        {
            MessageBox.Show(this, "Select and open a radio station before building.", "No radio station selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_slots.All(slot => slot.ReplacementPath is null))
        {
            MessageBox.Show(this, "Assign at least one track before building.", "Nothing to build", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Filter = "GTA RPF archive (*.rpf)|*.rpf",
            FileName = Path.GetFileName(rpfPath),
            InitialDirectory = Path.Combine(Path.GetDirectoryName(rpfPath)!, "GTARadioEditor Output"),
            Title = "Save rebuilt radio RPF"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        await RunOperationAsync(async (progress, cancellationToken) =>
        {
            var result = await _rpfService.BuildOutputAsync(rpfPath, dialog.FileName, _slots, progress, cancellationToken);
            AppendLog($"\r\nSuccess: {result.ReplacedContainers} container(s) rebuilt.\r\nOutput: {result.OutputRpfPath}");
            MessageBox.Show(this,
                $"Built {result.ReplacedContainers} replacement(s).\n\nCopy the output RPF into the matching path under your GTA V mods folder. Keep its file name unchanged.",
                "Build complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
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
            _status.Text = "Ready";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Cancelled";
            AppendLog("Operation cancelled.");
        }
        catch (Exception exception)
        {
            _status.Text = "Failed";
            AppendLog("ERROR: " + exception.Message);
            MessageBox.Show(this, exception.Message, "GTA Radio Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        foreach (var control in Controls.OfType<Control>()) control.Enabled = !busy;
        if (!busy)
        {
            _scanButton.Enabled = _stationSelector.SelectedItem is RadioStation;
            _buildButton.Enabled = _slots.Count > 0;
            _assignButton.Enabled = _tracks.Count > 0 && _slots.Count > 0;
        }
    }

    private void RefreshSlots()
    {
        _slotGrid.Refresh();
        _assignButton.Enabled = _tracks.Count > 0 && _slots.Count > 0;
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
