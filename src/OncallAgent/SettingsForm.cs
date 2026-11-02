using System.Net;
using Busy.Bar;
using OncallAgent.Config;

namespace OncallAgent;

/// <summary>The settings window: the BUSY Bar's address and HTTP API password, and whether the
/// microphone check runs. Everything else stays in the settings file, the registry or Intune.
/// Fields an admin manages through policy are shown but locked.</summary>
public sealed class SettingsForm : Form
{
    private const string Title = "BUSY Bar on call agent settings";
    private const string ManagedNote = "Managed by your organization";

    private readonly TextBox _address;
    private readonly TextBox _password;
    private readonly CheckBox _micEnabled;
    private readonly Button _testButton;
    private readonly Label _testResult;
    private readonly PolicyManagedSettings _managed;
    private readonly string? _managedPassword;
    private readonly Action<UserSettings> _save;
    private CancellationTokenSource? _testCancellation;

    /// <param name="save">Saves and applies the settings. If it throws, the window stays open and
    /// shows the error.</param>
    public SettingsForm(BusyBarOptions busyBar, MicOptions mic, PolicyManagedSettings managed, Action<UserSettings> save)
    {
        _managed = managed;
        _save = save;
        _managedPassword = managed.HttpAccessPassword ? busyBar.ResolveHttpAccessPassword() : null;

        Text = Title;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill
        };
        Controls.Add(layout);

        _address = new TextBox { Text = busyBar.Address, Width = 360, Enabled = !managed.Address };
        AddField(layout, "BUSY Bar address", _address, managed.Address
            ? ManagedNote
            : "10.0.4.20 is the USB connection. Over Wi-Fi, use the bar's IP address or hostname.");

        _password = new TextBox
        {
            Text = busyBar.ResolveHttpAccessPassword() ?? "",
            Width = 360,
            UseSystemPasswordChar = true,
            Enabled = !managed.HttpAccessPassword
        };
        AddField(layout, "HTTP API password", _password, managed.HttpAccessPassword
            ? ManagedNote
            : "Only needed over Wi-Fi, when the bar's HTTP API has a password. Leave it empty otherwise.");

        _testButton = new Button { Text = "Test connection", AutoSize = true };
        _testButton.Click += async (_, _) => await TestConnectionAsync().ConfigureAwait(true);
        // Hidden until there's a result, so it doesn't leave a blank line under the button.
        _testResult = new Label { AutoSize = true, MaximumSize = new Size(360, 0), Margin = new Padding(3, 6, 3, 3), Visible = false };
        layout.Controls.Add(_testButton);
        layout.Controls.Add(_testResult);

        _micEnabled = new CheckBox
        {
            Text = "Show me as on a call while an app is using my microphone",
            Checked = mic.Enabled,
            AutoSize = true,
            Enabled = !managed.MicEnabled,
            Margin = new Padding(3, 16, 3, 0)
        };
        layout.Controls.Add(_micEnabled);
        layout.Controls.Add(Hint(managed.MicEnabled
            ? ManagedNote
            : "Turn this off to rely on Teams presence alone."));

        var saveButton = new Button { Text = "Save", AutoSize = true };
        saveButton.Click += (_, _) => Save();
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 16, 0, 0)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(saveButton);
        layout.Controls.Add(buttons);

        AcceptButton = saveButton;
        CancelButton = cancel;
    }

    private static void AddField(TableLayoutPanel layout, string label, TextBox box, string hint)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        layout.Controls.Add(box);
        layout.Controls.Add(Hint(hint));
    }

    private static Label Hint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(360, 0),
        ForeColor = SystemColors.GrayText
    };

    private string? EnteredPassword() =>
        _managed.HttpAccessPassword ? _managedPassword : _password.Text.Length == 0 ? null : _password.Text;

    private async Task TestConnectionAsync()
    {
        var address = _address.Text.Trim();
        if (address.Length == 0)
        {
            ShowTestResult("Enter the bar's address first.", success: false);
            return;
        }

        _testCancellation?.Cancel();
        _testCancellation = new CancellationTokenSource();
        var cancellation = _testCancellation.Token;

        _testButton.Enabled = false;
        ShowTestResult($"Connecting to {address}…", success: null);
        try
        {
            using var bar = new BusyBar(new Busy.Bar.BusyBarOptions { Addr = address, HttpAccessPassword = EnteredPassword() });
            var name = await bar.NameGetAsync(cancellationToken: cancellation).ConfigureAwait(true);
            ShowTestResult($"Connected to {name.Name}.", success: true);
        }
        catch (BusyBarApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            ShowTestResult("The bar rejected the password. Check the HTTP API password in the BUSY app.", success: false);
        }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException)
        {
            ShowTestResult($"{address} isn't a valid address.", success: false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The window closed, or a newer test started.
        }
        catch (Exception)
        {
            ShowTestResult($"Couldn't reach a BUSY Bar at {address}. Check that it's connected and the address is right.", success: false);
        }
        finally
        {
            if (!IsDisposed)
            {
                _testButton.Enabled = true;
            }
        }
    }

    private void ShowTestResult(string message, bool? success)
    {
        if (IsDisposed)
        {
            return;
        }

        _testResult.Text = message;
        _testResult.Visible = true;
        _testResult.ForeColor = success switch
        {
            true => Color.FromArgb(0x10, 0x7C, 0x10),
            false => Color.FromArgb(0xC4, 0x2B, 0x1C),
            null => SystemColors.ControlText
        };
    }

    private void Save()
    {
        var address = _address.Text.Trim();
        if (!_managed.Address && address.Length == 0)
        {
            MessageBox.Show(this, "Enter the bar's address. For the USB connection, that's 10.0.4.20.", Title,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _address.Focus();
            return;
        }

        try
        {
            _save(new UserSettings(
                _managed.Address ? null : address,
                _managed.HttpAccessPassword ? null : _password.Text,
                _managed.MicEnabled ? null : _micEnabled.Checked));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't save your settings:\n\n{ex.Message}", Title,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _testCancellation?.Cancel();
        _testCancellation?.Dispose();
        base.OnFormClosed(e);
    }
}
