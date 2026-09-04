using System.Diagnostics;

namespace GTARadioEditor;

public sealed class UpdateAvailableDialog : Form
{
    private readonly string releaseUrl;

    public UpdateAvailableDialog(string currentVersion, string latestVersion, string releaseUrl)
    {
        this.releaseUrl = releaseUrl;
        Text = "Update Available";
        Font = SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, 170);
        var iconBox = new PictureBox
        {
            Image = SystemIcons.Information.ToBitmap(),
            SizeMode = PictureBoxSizeMode.AutoSize,
            Location = new Point(20, 20)
        };
        var messageLabel = new Label
        {
            Text = "A new version of GTA Gameconfig Updater is available.",
            AutoSize = true,
            Location = new Point(64, 20),
            MaximumSize = new Size(296, 0)
        };
        var currentVersionLabel = new Label
        {
            Text = $"Current version: {currentVersion}",
            AutoSize = true,
            Location = new Point(64, 56)
        };
        var newVersionLabel = new Label
        {
            Text = $"New version: {latestVersion}",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(64, 76)
        };
        var goButton = new Button
        {
            Text = "Take me there",
            Size = new Size(110, 32),
            Location = new Point(80, 120),
            UseVisualStyleBackColor = true
        };
        var dismissButton = new Button
        {
            Text = "I'm good",
            Size = new Size(110, 32),
            Location = new Point(198, 120),
            DialogResult = DialogResult.Cancel
        };
        goButton.Click += GoButton_Click;
        AcceptButton = goButton;
        CancelButton = dismissButton;
        Controls.Add(iconBox);
        Controls.Add(messageLabel);
        Controls.Add(currentVersionLabel);
        Controls.Add(newVersionLabel);
        Controls.Add(dismissButton);
        Controls.Add(goButton);
    }

    private void GoButton_Click(object? sender, EventArgs e)
    {
        OpenReleasePage();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void OpenReleasePage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(releaseUrl) { UseShellExecute = true });
        }
        catch
        {
            // 
        }
    }
}
