using System.IO;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// Basic information about the game, from the game section of the data.
/// </summary>
public sealed class GameInfo : IValidatedData
{
	/// <summary>
	/// The name of the data section the game info is read from.
	/// </summary>
	public const string SectionName = "game";

	/// <summary>
	/// Loads the game info from the data folder, without mods.
	/// </summary>
	public static DataLoadResult<GameInfo> Load(string dataDirectory)
	{
		var document = DataDocument.Read(dataDirectory, []);
		return document.Value is { } value ? Read(value) : DataLoadResult<GameInfo>.Failure(document.Errors);
	}

	/// <summary>
	/// Reads the game info from merged data.
	/// </summary>
	public static DataLoadResult<GameInfo> Read(DataDocument document)
	{
		var errors = new List<DataLoadError>();
		var gameInfo = document.ReadSection<GameInfo>(SectionName, errors);
		if (gameInfo is null && errors.Count == 0)
			errors.Add(document.CreateError(OurResources.DataSectionMissing.FormatCurrentCulture(SectionName)));
		if (gameInfo is not null)
			errors.AddRange(gameInfo.Validate().Select(document.CreateError));
		return errors.Count != 0 ? DataLoadResult<GameInfo>.Failure(errors) : DataLoadResult<GameInfo>.Success(gameInfo!);
	}

	public required string Title { get; init; }

	public required LongText Description { get; init; }

	/// <summary>
	/// The application icon's path, relative to the data folder. Optional.
	/// </summary>
	public string? Icon { get; init; }

	/// <summary>
	/// The chances that items chosen at random are uncommon or rare.
	/// </summary>
	public FrequencyChances FrequencyChances { get; init; } = new();

	/// <summary>
	/// A percentage chance. Any character value that has this chance or less of being chosen is reported as a warning
	/// when the data is loaded. Optional.
	/// </summary>
	public double? LowChanceWarning { get; init; }

	/// <summary>
	/// Gets the full path of the icon file, or null if there is no icon.
	/// </summary>
	public string? GetIconPath(string dataDirectory) => Icon is null ? null : Path.GetFullPath(Path.Combine(dataDirectory, Icon));

	public IEnumerable<string> Validate()
	{
		if (string.IsNullOrWhiteSpace(Title))
			yield return OurResources.GameInfoTitleMissing;
		if (string.IsNullOrWhiteSpace(Description.Text))
			yield return OurResources.GameInfoDescriptionMissing;
		if (Icon is not null && (string.IsNullOrWhiteSpace(Icon) || Icon.IndexOfAny(Path.GetInvalidPathChars()) != -1 || Path.IsPathRooted(Icon)))
			yield return OurResources.GameInfoIconInvalid.FormatCurrentCulture(Icon);
		foreach (var error in FrequencyChances.Validate("frequencyChances"))
			yield return error;
		if (LowChanceWarning is { } lowChanceWarning && !(lowChanceWarning >= 0 && lowChanceWarning < 100))
			yield return OurResources.LowChanceWarningInvalid.FormatCurrentCulture(lowChanceWarning);
	}
}
