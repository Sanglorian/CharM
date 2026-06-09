namespace CharM.Web.Services;

public sealed class DiceRollerUiService
{
    public event Action? Changed;

    public string Expression { get; private set; } = "1d20";
    public string? Label { get; private set; }
    public bool IsOpen { get; private set; }
    public IReadOnlyList<DiceRollResult> History => _history;

    private readonly List<DiceRollResult> _history = [];

    public void Open(string expression = "1d20", string? label = null)
    {
        Expression = expression;
        Label = label;
        IsOpen = true;
        Changed?.Invoke();
    }

    public void Close()
    {
        IsOpen = false;
        Changed?.Invoke();
    }

    public void Record(DiceRollResult result)
    {
        _history.Insert(0, result);
        if (_history.Count > 12)
            _history.RemoveRange(12, _history.Count - 12);
        Changed?.Invoke();
    }
}
