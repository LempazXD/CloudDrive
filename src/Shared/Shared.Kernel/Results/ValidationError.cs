namespace Shared.Kernel.Results;

public sealed record ValidationError : Error
{
	public IReadOnlyList<ValidationFailure> Failures { get; }

	internal ValidationError(string code, IReadOnlyList<ValidationFailure> failures)
		: base(code, ErrorType.Validation)
	{
		if (failures.Count == 0)
			throw new ArgumentException("ValidationError must carry at least one failure.", nameof(failures));

		Failures = failures.Distinct()
			.OrderBy(failure => failure.PropertyName, StringComparer.Ordinal)
			.ThenBy(failure => failure.ReasonCode, StringComparer.Ordinal)
			.ToArray();
	}

	public bool Equals(ValidationError? other) =>
		other is not null
		&& Code == other.Code
		&& Type == other.Type
		&& Failures.SequenceEqual(other.Failures);

	public override int GetHashCode()
	{
		var hash = new HashCode();
		hash.Add(Code);
		hash.Add(Type);
		foreach (var failure in Failures)
			hash.Add(failure);
		return hash.ToHashCode();
	}
}

/// <summary> Одно нарушение валидации: <paramref name="PropertyName"/> — поле, <paramref name="ReasonCode"/> — код причины (ключ локализации). </summary>
public sealed record ValidationFailure(string PropertyName, string ReasonCode);
