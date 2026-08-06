using BusyBarOncallAgent.Graph;
using BusyBarOncallAgent.Mic;
using BusyBarOncallAgent.OnCall;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusyBarOncallAgent;

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
    private readonly ILogger<TrayApplicationContext> _logger;

    private GraphPresenceSnapshot? _latestPresence;
    private DateTimeOffset? _latestMeetingEnd;
    private bool _latestMicActive;
    private bool _pollingPaused;

    public TrayApplicationContext(
        IGraphPresenceClient presenceClient,
        IGraphAuthService authService,
        MicrophoneActivityDetector micDetector,
        BusyBarTimerController timerController,
        IOptions<GraphOptions> graphOptions,
        IOptions<MicOptions> micOptions,
        ILogger<TrayApplicationContext> logger)
    {
        _presenceClient = presenceClient;
        _authService = authService;
        _micDetector = micDetector;
        _timerController = timerController;
        _logger = logger;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Sign in", null, async (_, _) => await SignInAsync().ConfigureAwait(true));
        menu.Items.Add("Sign out", null, async (_, _) => await _authService.SignOutAsync().ConfigureAwait(true));
        menu.Items.Add("Pause", null, (_, _) => _pollingPaused = !_pollingPaused);
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
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
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graph poll failed; keeping last known presence.");
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
        try
        {
            await _timerController.ApplyAsync(state, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply BUSY Bar timer state.");
        }
    }

    private async Task SignInAsync()
    {
        try
        {
            await _authService.GetAccessTokenAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sign in failed.");
        }
    }
}
