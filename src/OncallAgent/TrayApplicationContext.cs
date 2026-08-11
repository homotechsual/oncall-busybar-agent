using OncallAgent.Graph;
using OncallAgent.Mic;
using OncallAgent.OnCall;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OncallAgent;

/// <summary>The tray application's shell: a NotifyIcon with no main window, hosting the two
/// polling timers and applying the resulting OnCallState to the BUSY Bar. Both timers run on
/// this class's own thread (the WinForms UI thread), since the Win32 APIs behind the tray icon
/// and the registry reads need to run there.</summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _graphTimer;
    private readonly System.Windows.Forms.Timer _micTimer;
    private readonly IGraphPresenceClient _presenceClient;
    private readonly IGraphAuthService _authService;
    private readonly MicrophoneActivityDetector _micDetector;
    private readonly BusyBarTimerController _timerController;
    private readonly IStartupShortcutManager _startupShortcutManager;
    private readonly ILogger<TrayApplicationContext> _logger;

    private GraphPresenceSnapshot? _latestPresence;
    private DateTimeOffset? _latestMeetingEnd;
    private bool _latestMicActive;
    private bool _pollingPaused;
    private bool _lastGraphPollFailed;
    private bool _suppressStartupCheckedChanged;

    public TrayApplicationContext(
        IGraphPresenceClient presenceClient,
        IGraphAuthService authService,
        MicrophoneActivityDetector micDetector,
        BusyBarTimerController timerController,
        IStartupShortcutManager startupShortcutManager,
        IOptions<GraphOptions> graphOptions,
        IOptions<MicOptions> micOptions,
        ILogger<TrayApplicationContext> logger)
    {
        _presenceClient = presenceClient;
        _authService = authService;
        _micDetector = micDetector;
        _timerController = timerController;
        _startupShortcutManager = startupShortcutManager;
        _logger = logger;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Sign in", null, async (_, _) => await SignInAsync().ConfigureAwait(true));
        menu.Items.Add("Sign out", null, async (_, _) => await SignOutAsync().ConfigureAwait(true));
        menu.Items.Add("Pause", null, (_, _) => _pollingPaused = !_pollingPaused);

        var startupItem = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true };
        menu.Items.Add(startupItem);
        _ = InitializeStartupItemAsync(startupItem);

        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _icon = new NotifyIcon
        {
            // Uses the exe's own embedded icon (set via <ApplicationIcon> in the csproj) rather
            // than a separate file lookup, so the tray icon and the exe's file icon always match
            // and stay in sync automatically.
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Text = "BUSY Bar on call agent",
            ContextMenuStrip = menu,
            Visible = true
        };

        _graphTimer = new System.Windows.Forms.Timer { Interval = graphOptions.Value.PollIntervalSeconds * 1000 };
        _graphTimer.Tick += async (_, _) => await PollGraphAsync().ConfigureAwait(true);
        _graphTimer.Start();

        _micTimer = new System.Windows.Forms.Timer { Interval = micOptions.Value.PollIntervalSeconds * 1000 };
        _micTimer.Tick += async (_, _) => await PollMicrophoneAsync().ConfigureAwait(true);
        if (micOptions.Value.Enabled)
        {
            _micTimer.Start();
        }
    }

    private async Task PollGraphAsync()
    {
        if (_pollingPaused)
        {
            return;
        }

        try
        {
            _latestPresence = await _presenceClient.GetPresenceAsync(CancellationToken.None).ConfigureAwait(true);
            _latestMeetingEnd = await _presenceClient.GetCurrentMeetingEndAsync(CancellationToken.None).ConfigureAwait(true);
            _lastGraphPollFailed = false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graph poll failed; keeping last known presence.");
            _lastGraphPollFailed = true;
        }

        await ApplyStateAsync().ConfigureAwait(true);
    }

    private async Task PollMicrophoneAsync()
    {
        if (_pollingPaused)
        {
            return;
        }

        try
        {
            _latestMicActive = _micDetector.IsActive();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Microphone check failed; treating as inactive.");
            _latestMicActive = false;
        }

        await ApplyStateAsync().ConfigureAwait(true);
    }

    private async Task ApplyStateAsync()
    {
        var state = OnCallStateEvaluator.Evaluate(_latestPresence, _latestMeetingEnd, _latestMicActive);
        _icon.Text = DescribeState(state);
        try
        {
            await _timerController.ApplyAsync(state, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply BUSY Bar timer state.");
        }
    }

    private string DescribeState(OnCallState state)
    {
        var summary = state switch
        {
            OnCallUntil until => $"BUSY Bar: on call until {until.EndUtc.ToLocalTime():HH:mm}",
            OnCallIndefinite => "BUSY Bar: on call",
            _ => "BUSY Bar: idle"
        };

        return _lastGraphPollFailed
            ? $"{summary} (last Graph poll failed — signed out?)"
            : summary;
    }

    private async Task SignInAsync()
    {
        try
        {
            await _authService.GetAccessTokenAsync(CancellationToken.None).ConfigureAwait(true);
            if (!_graphTimer.Enabled)
            {
                _graphTimer.Start();
            }
            _icon.ShowBalloonTip(5000, "BUSY Bar on call agent", "Signed in.", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            // The packaged app has no console, so a log-only failure is invisible to the user —
            // a balloon tip is the only way a background tray app can surface an error at all.
            _logger.LogWarning(ex, "Sign in failed.");
            _icon.ShowBalloonTip(5000, "BUSY Bar on call agent", $"Sign in failed: {ex.Message}", ToolTipIcon.Error);
        }
    }

    private async Task SignOutAsync()
    {
        try
        {
            await _authService.SignOutAsync().ConfigureAwait(true);
            _icon.ShowBalloonTip(5000, "BUSY Bar on call agent", "Signed out.", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sign out failed.");
            _icon.ShowBalloonTip(5000, "BUSY Bar on call agent", $"Sign out failed: {ex.Message}", ToolTipIcon.Error);
        }
        finally
        {
            _graphTimer.Stop();
        }
    }

    private async Task InitializeStartupItemAsync(ToolStripMenuItem startupItem)
    {
        try
        {
            startupItem.Checked = await _startupShortcutManager.IsEnabledAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read startup shortcut state.");
        }

        startupItem.CheckedChanged += async (_, _) =>
        {
            if (_suppressStartupCheckedChanged)
            {
                return;
            }

            try
            {
                var succeeded = startupItem.Checked
                    ? await _startupShortcutManager.EnableAsync().ConfigureAwait(true)
                    : await _startupShortcutManager.DisableAsync().ConfigureAwait(true);

                if (!succeeded)
                {
                    _suppressStartupCheckedChanged = true;
                    startupItem.Checked = !startupItem.Checked;
                    _suppressStartupCheckedChanged = false;
                    _icon.ShowBalloonTip(5000, "BUSY Bar on call agent",
                        "Windows didn't apply that change — startup may be managed by policy.", ToolTipIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update startup shortcut.");
            }
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _graphTimer.Stop();
            _graphTimer.Dispose();
            _micTimer.Stop();
            _micTimer.Dispose();
            _icon.Visible = false;
            _icon.Dispose();
        }

        base.Dispose(disposing);
    }
}
