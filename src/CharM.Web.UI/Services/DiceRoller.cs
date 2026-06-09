using System.Text.RegularExpressions;

namespace CharM.Web.Services;

public sealed partial class DiceRoller
{
    private readonly Random _random;

    public DiceRoller()
        : this(Random.Shared)
    {
    }

    public DiceRoller(Random random)
    {
        _random = random;
    }

    public DiceRollResult Roll(string expression, string? label = null)
    {
        var parsed = DiceExpression.Parse(expression);
        var terms = new List<DiceRollTermResult>();
        int total = 0;

        foreach (var term in parsed.Terms)
        {
            if (term.DieSides is int sides)
            {
                var rolls = new List<int>(term.Count);
                for (int i = 0; i < term.Count; i++)
                    rolls.Add(_random.Next(1, sides + 1));

                int signedTotal = term.Sign * rolls.Sum();
                total += signedTotal;
                terms.Add(new DiceRollTermResult(term, rolls, signedTotal));
            }
            else
            {
                int signedTotal = term.Sign * term.Constant;
                total += signedTotal;
                terms.Add(new DiceRollTermResult(term, [], signedTotal));
            }
        }

        return new DiceRollResult(parsed, label, terms, total, DateTimeOffset.Now);
    }
}

public sealed partial record DiceExpression(IReadOnlyList<DiceTerm> Terms)
{
    public string NormalizedText
    {
        get
        {
            var parts = new List<string>();
            foreach (var term in Terms)
            {
                string text = term.DieSides is int sides
                    ? $"{term.Count}d{sides}"
                    : term.Constant.ToString();

                if (parts.Count == 0)
                {
                    parts.Add(term.Sign < 0 ? "-" + text : text);
                }
                else
                {
                    parts.Add(term.Sign < 0 ? " - " + text : " + " + text);
                }
            }
            return string.Concat(parts);
        }
    }

    public static DiceExpression Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new DiceExpressionException("Enter a dice expression.");

        string compact = WhitespaceRegex().Replace(expression.Trim(), "");
        if (compact.Length == 0)
            throw new DiceExpressionException("Enter a dice expression.");

        if (compact[0] is not '+' and not '-')
            compact = "+" + compact;

        var terms = new List<DiceTerm>();
        int index = 0;
        while (index < compact.Length)
        {
            char signChar = compact[index];
            if (signChar is not '+' and not '-')
                throw new DiceExpressionException($"Expected '+' or '-' at position {index + 1}.");
            int sign = signChar == '-' ? -1 : 1;
            index++;

            int start = index;
            while (index < compact.Length && compact[index] is not '+' and not '-')
                index++;

            string token = compact[start..index];
            if (token.Length == 0)
                throw new DiceExpressionException("Missing dice term.");

            var diceMatch = DiceTermRegex().Match(token);
            if (diceMatch.Success)
            {
                int count = diceMatch.Groups["count"].Success && diceMatch.Groups["count"].Value.Length > 0
                    ? int.Parse(diceMatch.Groups["count"].Value)
                    : 1;
                int sides = int.Parse(diceMatch.Groups["sides"].Value);
                if (count <= 0 || count > 100)
                    throw new DiceExpressionException("Dice count must be between 1 and 100.");
                if (sides <= 1 || sides > 1000)
                    throw new DiceExpressionException("Die sides must be between 2 and 1000.");
                terms.Add(new DiceTerm(sign, count, sides, 0));
                continue;
            }

            if (int.TryParse(token, out int constant))
            {
                terms.Add(new DiceTerm(sign, 0, null, constant));
                continue;
            }

            throw new DiceExpressionException($"Invalid dice term '{token}'.");
        }

        if (!terms.Any(t => t.DieSides is not null))
            throw new DiceExpressionException("Expression must include at least one die.");

        return new DiceExpression(terms);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^(?<count>\d*)d(?<sides>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex DiceTermRegex();
}

public sealed record DiceTerm(int Sign, int Count, int? DieSides, int Constant);

public sealed record DiceRollTermResult(DiceTerm Term, IReadOnlyList<int> Rolls, int Total);

public sealed record DiceRollResult(
    DiceExpression Expression,
    string? Label,
    IReadOnlyList<DiceRollTermResult> Terms,
    int Total,
    DateTimeOffset RolledAt)
{
    public int? SingleD20Raw => Terms.Count == 1
        ? null
        : Terms.FirstOrDefault(t => t.Term.DieSides == 20 && t.Term.Count == 1)?.Rolls.FirstOrDefault();

    public int? D20Raw => Terms.FirstOrDefault(t => t.Term.DieSides == 20 && t.Term.Count == 1)?.Rolls.FirstOrDefault();

    public int Modifier => Total - (D20Raw ?? Total);
}

public sealed class DiceExpressionException : Exception
{
    public DiceExpressionException(string message)
        : base(message)
    {
    }
}
