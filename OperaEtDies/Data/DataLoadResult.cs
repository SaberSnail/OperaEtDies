namespace OperaEtDies.Data;

/// <summary>
/// The outcome of loading a data file. Value is null if the file couldn't be loaded.
/// </summary>
public sealed class DataLoadResult<T>
	where T : class
{
	public static DataLoadResult<T> Success(T value) => new(value, []);

	public static DataLoadResult<T> Failure(IReadOnlyList<DataLoadError> errors) => new(null, errors);

	public static DataLoadResult<T> Failure(DataLoadError error) => new(null, [error]);

	private DataLoadResult(T? value, IReadOnlyList<DataLoadError> errors)
	{
		Value = value;
		Errors = errors;
	}

	public T? Value { get; }

	public IReadOnlyList<DataLoadError> Errors { get; }

	public bool Succeeded => Value is not null;
}
