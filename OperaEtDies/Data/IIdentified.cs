namespace OperaEtDies.Data;

/// <summary>
/// A data item with an id that's unique among items of its kind.
/// </summary>
public interface IIdentified
{
	string Id { get; }
}
