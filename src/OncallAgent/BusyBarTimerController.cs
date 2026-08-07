using Busy.Bar;
using OncallAgent.OnCall;

namespace OncallAgent;

/// <summary>Turns an <see cref="OnCallState"/> into BUSY timer API calls, tracking the last
/// state actually applied so it does not restart the device's timer (and its animation) on every
/// poll while nothing has meaningfully changed.</summary>
public sealed class BusyBarTimerController
{
    // A device's two BUSY timer profile slots have fixed, firmware-assigned IDs, not
    // user/caller-chosen ones: BusySnapshotSetAsync rejects any other CardId with
    // "400 Bad Request: Failed to parse snapshot" (confirmed against a real device). This is the
    // "Busy" slot's ID (BusyProfileSlot.Busy, title "BUSY") — the built-in profile, appropriate
    // for an automated on-call indicator rather than the user-configurable "Custom" slot.
    private const string CardId = "00000000-0000-0000-0000-000000000000";
    private static readonly TimeSpan ReapplyThreshold = TimeSpan.FromSeconds(60);

    private readonly BusyBar _bar;
    private OnCallState _lastApplied = new NotOnCall();

    public BusyBarTimerController(BusyBar bar)
    {
        _bar = bar;
    }

    public async Task ApplyAsync(OnCallState state, CancellationToken cancellationToken)
    {
        if (!NeedsReapply(_lastApplied, state))
        {
            return;
        }

        var snapshotState = ToSnapshotState(state);
        var snapshot = new BusySnapshot(snapshotState, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await _bar.BusySnapshotSetAsync(snapshot, cancellationToken: cancellationToken).ConfigureAwait(false);
        _lastApplied = state;
    }

    internal static bool NeedsReapply(OnCallState previous, OnCallState next) => (previous, next) switch
    {
        (NotOnCall, NotOnCall) => false,
        (OnCallIndefinite, OnCallIndefinite) => false,
        (OnCallUntil prev, OnCallUntil nxt) => (nxt.EndUtc - prev.EndUtc).Duration() > ReapplyThreshold,
        _ => true
    };

    private static BusySnapshotState ToSnapshotState(OnCallState state) => state switch
    {
        NotOnCall => new BusySnapshotNotStarted(),
        OnCallIndefinite => new BusySnapshotInfinite { CardId = CardId, IsPaused = false },
        OnCallUntil until => new BusySnapshotSimple
        {
            CardId = CardId,
            TimeLeftMs = Math.Max(0, (long)(until.EndUtc - DateTimeOffset.UtcNow).TotalMilliseconds),
            IsPaused = false
        },
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}
