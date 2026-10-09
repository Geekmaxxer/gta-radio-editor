using System.Diagnostics;

namespace GTARadioEditor;

public sealed class UpdateAvailableDialog : Form
{
    private readonly string releaseUrl;

    public UpdateAvailableDialog(string currentVersion, string latestVersion, string releaseUrl)
    {
        this.releaseUrl = releaseUrl;
        Text = L.T("Update Available");
        Font = SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 240);
        var iconBox = new PictureBox
        {
            Image = SystemIcons.Information.ToBitmap(),
            SizeMode = PictureBoxSizeMode.AutoSize,
            Location = new Point(20, 20)
        };
        var messageLabel = new Label
        {
            Text = L.T("A new version of GTA Radio Editor is available."),
            AutoSize = true,
            Location = new Point(64, 20),
            MaximumSize = new Size(470, 0)
        };
        var currentVersionLabel = new Label
        {
            Text = L.T("Current version: {0}", currentVersion),
            AutoSize = true,
            Location = new Point(64, 100)
        };
        var newVersionLabel = new Label
        {
            Text = L.T("New version: {0}", latestVersion),
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(64, 126)
        };
        var goButton = new Button
        {
            Text = L.T("Take me there"),
            AutoSize = true,
            MinimumSize = new Size(140, 34),
            Location = new Point(64, 180),
            UseVisualStyleBackColor = true
        };
        var dismissButton = new Button
        {
            Text = L.T("I'm good"),
            AutoSize = true,
            MinimumSize = new Size(140, 34),
            Location = new Point(330, 180),
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
            // Keep the dialog usable if Windows cannot open the release page.
        }
    }
}
