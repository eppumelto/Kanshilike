using System;

/// <summary>
/// Names a faction by its stable id string. Rules code only ever sees this, never the
/// FactionDefinition asset. Equality is ordinal and case-sensitive; null and "" are None.
/// </summary>
public readonly struct FactionId : IEquatable<FactionId>
{
	public static readonly FactionId None = default;

	public readonly string Value;

	public FactionId(string value)
	{
		Value = string.IsNullOrEmpty(value) ? null : value;
	}

	public bool IsNone => string.IsNullOrEmpty(Value);

	public bool Equals(FactionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
	public override bool Equals(object obj) => obj is FactionId other && Equals(other);
	public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
	public static bool operator ==(FactionId a, FactionId b) => a.Equals(b);
	public static bool operator !=(FactionId a, FactionId b) => !a.Equals(b);
	public override string ToString() => IsNone ? "none" : Value;
}
