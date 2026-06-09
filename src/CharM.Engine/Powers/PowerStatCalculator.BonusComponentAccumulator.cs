using System.Text;
using System.Text.RegularExpressions;
using CharM.Engine.Evaluation;
using CharM.Engine.Rules;

namespace CharM.Engine.Powers;

public static partial class PowerStatCalculator
{
    private sealed class BonusComponentAccumulator
    {
        private readonly List<string> _components = [];
        private readonly Dictionary<string, TypedEntry> _typedValues = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TypedEntry> _suppressedTypedValues = new(StringComparer.OrdinalIgnoreCase);
        private readonly bool _displaySuppressedTypedBonuses;

        public BonusComponentAccumulator(bool displaySuppressedTypedBonuses = false)
        {
            _displaySuppressedTypedBonuses = displaySuppressedTypedBonuses;
        }

        public int Total { get; private set; }

        public string Components => string.Concat(_components);

        public void Seed(int amount, string? bonusType)
        {
            if (string.IsNullOrWhiteSpace(bonusType))
                return;

            _typedValues[bonusType] = new TypedEntry(amount, null, null);
        }

        public void AddDisplayOnlyContribution(
            StatContribution contribution,
            Func<string, string?>? sourceNameResolver)
            => _components.Add(FormatBonusComponent(contribution, 0, sourceNameResolver));

        public void AddContribution(
            StatContribution contribution,
            int amount,
            Func<string, string?>? sourceNameResolver)
        {
            string? bonusType = contribution.BonusType;
            if (string.IsNullOrWhiteSpace(bonusType))
            {
                Total += amount;
                _components.Add(FormatBonusComponent(contribution, amount, sourceNameResolver));
                return;
            }

            if (!_typedValues.TryGetValue(bonusType, out var current))
            {
                _typedValues[bonusType] = new TypedEntry(amount, _components.Count, contribution.SourceElementId);
                Total += amount;
                _components.Add(FormatBonusComponent(contribution, amount, sourceNameResolver));
                return;
            }

            if (!ShouldReplaceTypedBonus(current.Amount, amount))
            {
                if (_displaySuppressedTypedBonuses
                    && amount > 0
                    && !string.Equals(current.SourceElementId, contribution.SourceElementId, StringComparison.OrdinalIgnoreCase))
                {
                    AddSuppressedTypedBonus(contribution, amount, sourceNameResolver, bonusType);
                }
                return;
            }

            Total += amount - current.Amount;
            string replacement = FormatBonusComponent(contribution, amount, sourceNameResolver);
            if (current.ComponentIndex is { } componentIndex)
            {
                _components[componentIndex] = replacement;
                _typedValues[bonusType] = new TypedEntry(amount, componentIndex, contribution.SourceElementId);
                return;
            }

            _typedValues[bonusType] = new TypedEntry(amount, _components.Count, contribution.SourceElementId);
            _components.Add(replacement);
        }

        private static bool ShouldReplaceTypedBonus(int current, int candidate)
        {
            if (candidate >= 0)
                return current <= 0 || candidate > current;

            return current < 0 && candidate < current;
        }

        private void AddSuppressedTypedBonus(
            StatContribution contribution,
            int amount,
            Func<string, string?>? sourceNameResolver,
            string bonusType)
        {
            string sourceKey = GetSourceKey(contribution, sourceNameResolver);
            string key = bonusType + "|" + sourceKey;
            if (_suppressedTypedValues.TryGetValue(key, out var current))
            {
                if (!ShouldReplaceTypedBonus(current.Amount, amount))
                    return;

                if (current.ComponentIndex is { } componentIndex)
                    _components[componentIndex] = FormatBonusComponent(contribution, amount, sourceNameResolver, doesntStack: true);
                _suppressedTypedValues[key] = new TypedEntry(amount, current.ComponentIndex, contribution.SourceElementId);
                return;
            }

            _suppressedTypedValues[key] = new TypedEntry(amount, _components.Count, contribution.SourceElementId);
            _components.Add(FormatBonusComponent(contribution, amount, sourceNameResolver, doesntStack: true));
        }

        private static string GetSourceKey(StatContribution contribution, Func<string, string?>? sourceNameResolver)
        {
            if (!string.IsNullOrWhiteSpace(contribution.SourceElementId))
            {
                string? sourceName = sourceNameResolver?.Invoke(contribution.SourceElementId);
                return string.IsNullOrWhiteSpace(sourceName)
                    ? contribution.SourceElementId
                    : sourceName;
            }

            return "";
        }

        private readonly record struct TypedEntry(int Amount, int? ComponentIndex, string? SourceElementId);
    }
}
