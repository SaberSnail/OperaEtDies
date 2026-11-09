using GoldenAnvil.Utility;

namespace OperaEtDies.Utility.Random;

/// <summary>
/// A value that starts out random and can be randomized again. The value can also be predefined by setting it,
/// and locked so that randomizing leaves it alone.
/// </summary>
public sealed class RandomizedValue<T>
{
	/// <summary>
	/// Creates the value, picking its starting value right away.
	/// </summary>
	public RandomizedValue(Func<T> pickRandom)
	{
		m_pickRandom = pickRandom;
		m_value = pickRandom();
	}

	/// <summary>
	/// Raised when the value changes, whether it was set or randomized.
	/// </summary>
	public event EventHandler? ValueChanged;

	/// <summary>
	/// The current value. Setting it works even when the value is locked.
	/// </summary>
	public T Value
	{
		get => m_value;
		set
		{
			if (EqualityComparer<T>.Default.Equals(m_value, value))
				return;

			m_value = value;
			ValueChanged.Raise(this);
		}
	}

	/// <summary>
	/// Whether randomizing leaves the value alone.
	/// </summary>
	public bool IsLocked { get; set; }

	/// <summary>
	/// Sets the value and locks it.
	/// </summary>
	public void Lock(T value)
	{
		Value = value;
		IsLocked = true;
	}

	/// <summary>
	/// Picks a new random value, unless the value is locked. The new value may be the same as the old one.
	/// </summary>
	public void Randomize()
	{
		if (!IsLocked)
			Value = m_pickRandom();
	}

	private readonly Func<T> m_pickRandom;
	private T m_value;
}
