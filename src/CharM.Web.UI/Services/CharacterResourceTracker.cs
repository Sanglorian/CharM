using CharM.Engine.Creation;

namespace CharM.Web.Services;

public sealed class CharacterResourceTracker
{
    private long _sessionVersion = -1;
    private CharacterResourceState _state = new();

    public event Action? Changed;

    public CharacterResourceState GetState(CharacterSessionService sessionService)
    {
        EnsureSession(sessionService);
        return _state;
    }

    public void ApplyDamage(CharacterSessionService sessionService, int damage)
    {
        if (damage <= 0) return;
        EnsureSession(sessionService);

        _state = ResourceMath.ApplyDamage(_state, damage);
        NotifyChanged();
    }

    public void Heal(CharacterSessionService sessionService, int amount)
    {
        if (amount <= 0) return;
        EnsureSession(sessionService);
        _state = ResourceMath.Heal(_state, amount);
        NotifyChanged();
    }

    public void SetTempHp(CharacterSessionService sessionService, int amount)
    {
        EnsureSession(sessionService);
        _state = _state with { TempHp = Math.Max(0, amount) };
        NotifyChanged();
    }

    public void SpendSurge(CharacterSessionService sessionService)
    {
        EnsureSession(sessionService);
        if (_state.SpentSurges >= _state.MaxSurges) return;
        _state = _state with { SpentSurges = _state.SpentSurges + 1 };
        NotifyChanged();
    }

    public void RecoverSurge(CharacterSessionService sessionService)
    {
        EnsureSession(sessionService);
        if (_state.SpentSurges <= 0) return;
        _state = _state with { SpentSurges = _state.SpentSurges - 1 };
        NotifyChanged();
    }

    public void SpendPowerPoint(CharacterSessionService sessionService, int amount = 1)
    {
        if (amount <= 0) return;
        EnsureSession(sessionService);
        _state = _state with { SpentPowerPoints = Math.Min(_state.MaxPowerPoints, _state.SpentPowerPoints + amount) };
        NotifyChanged();
    }

    public void RecoverPowerPoint(CharacterSessionService sessionService, int amount = 1)
    {
        if (amount <= 0) return;
        EnsureSession(sessionService);
        _state = _state with { SpentPowerPoints = Math.Max(0, _state.SpentPowerPoints - amount) };
        NotifyChanged();
    }

    public void MarkDeathSave(CharacterSessionService sessionService)
    {
        EnsureSession(sessionService);
        _state = _state with { FailedDeathSaves = Math.Clamp(_state.FailedDeathSaves + 1, 0, 3) };
        NotifyChanged();
    }

    public void ClearDeathSaves(CharacterSessionService sessionService)
    {
        EnsureSession(sessionService);
        _state = _state with { FailedDeathSaves = 0 };
        NotifyChanged();
    }

    public void ShortRest(CharacterSessionService sessionService)
    {
        EnsureSession(sessionService);
        _state = _state with
        {
            TempHp = 0,
            FailedDeathSaves = 0,
            SpentPowerPoints = 0,
        };
        NotifyChanged();
    }

    public void ExtendedRest(CharacterSessionService sessionService)
    {
        EnsureSession(sessionService);
        _state = _state with
        {
            CurrentHp = _state.MaxHp,
            TempHp = 0,
            SpentSurges = 0,
            SpentPowerPoints = 0,
            FailedDeathSaves = 0,
        };
        NotifyChanged();
    }

    private void EnsureSession(CharacterSessionService sessionService)
    {
        if (_sessionVersion == sessionService.SessionVersion)
            return;

        _sessionVersion = sessionService.SessionVersion;
        var session = sessionService.Session;
        var snapshot = session?.GetPartialSnapshot();
        int maxHp = Math.Max(0, snapshot?.GetStat("Hit Points") ?? 0);
        int maxSurges = Math.Max(0, snapshot?.GetStat("Healing Surges") ?? 0);
        int maxPowerPoints = Math.Max(0, snapshot?.GetStat("Power Points") ?? 0);

        _state = new CharacterResourceState(
            MaxHp: maxHp,
            CurrentHp: maxHp,
            TempHp: 0,
            MaxSurges: maxSurges,
            SpentSurges: 0,
            MaxPowerPoints: maxPowerPoints,
            SpentPowerPoints: 0,
            FailedDeathSaves: 0);
    }

    private void NotifyChanged() => Changed?.Invoke();
}

public static class ResourceMath
{
    public static CharacterResourceState ApplyDamage(CharacterResourceState state, int damage)
    {
        int remaining = Math.Max(0, damage);
        int tempHp = state.TempHp;
        if (tempHp > 0)
        {
            int absorbed = Math.Min(tempHp, remaining);
            tempHp -= absorbed;
            remaining -= absorbed;
        }

        return state with
        {
            TempHp = tempHp,
            CurrentHp = Math.Max(0, state.CurrentHp - remaining),
        };
    }

    public static CharacterResourceState Heal(CharacterResourceState state, int amount)
        => state with { CurrentHp = Math.Min(state.MaxHp, state.CurrentHp + Math.Max(0, amount)) };
}

public sealed record CharacterResourceState(
    int MaxHp = 0,
    int CurrentHp = 0,
    int TempHp = 0,
    int MaxSurges = 0,
    int SpentSurges = 0,
    int MaxPowerPoints = 0,
    int SpentPowerPoints = 0,
    int FailedDeathSaves = 0)
{
    public int LostHp => Math.Max(0, MaxHp - CurrentHp);
    public int RemainingSurges => Math.Max(0, MaxSurges - SpentSurges);
    public int RemainingPowerPoints => Math.Max(0, MaxPowerPoints - SpentPowerPoints);
    public int SurgeValue => MaxHp / 4;
}
