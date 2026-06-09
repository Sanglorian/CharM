namespace CharM.Web.Services;

/// <summary>
/// Scoped per-circuit state used by <c>CharacterPersistence</c> to communicate
/// the active restore phase to the rest of the UI (the loading overlay and
/// the Home new-character gate). Default is <see cref="RestorePhase.Pending"/>
/// so consumers can suppress "no character" UI until we've actually checked
/// localStorage on the first interactive render.
/// </summary>
public sealed class CharacterRestoreState
{
    private RestorePhase _phase = RestorePhase.Pending;

    public event Action? Changed;

    public RestorePhase Phase => _phase;

    public bool IsBlockingUi => _phase is RestorePhase.Pending or RestorePhase.Restoring;

    public void Set(RestorePhase phase)
    {
        if (_phase == phase) return;
        _phase = phase;
        Changed?.Invoke();
    }
}

public enum RestorePhase
{
    /// <summary>First interactive render hasn't run yet; we don't know if a saved character exists.</summary>
    Pending,
    /// <summary>localStorage had a saved character and we're actively importing it.</summary>
    Restoring,
    /// <summary>Restore complete (or no saved character) — normal UI may render.</summary>
    Idle,
}
