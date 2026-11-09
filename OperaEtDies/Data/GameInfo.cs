using System.IO;
using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// Basic information about the game, loaded from GameInfo.json.
/// </summary>
public sealed class GameInfo : IValidatedData
{
	public const string FileName = "GameInfo.json";

	public static DataLoadResult<GameInfo> Load(string dataDirectory) =>
		JsonDataLoader.Load<GameInfo>(Path.Combine(dataDirectory, FileName));

	public required string Title { get; init; }

	public required string Description { get; init; }

	/// <summary>
	/// The application icon's path, relative to the data folder. Optional.
	/// </summary>
	public string? Icon { get; init; }

	/// <summary>
	/// Gets the full path of the icon file, or null if there is no icon.
	/// </summary>
	public string? GetIconPath(string dataDirectory) => Icon is null ? null : Path.GetFullPath(Path.Combine(dataDirectory, Icon));

	public IEnumerable<string> Validate()
	{
		if (string.IsNullOrWhiteSpace(Title))
			yield return OurResources.GameInfoTitleMissing;
		if (string.IsNullOrWhiteSpace(Description))
			yield return OurResources.GameInfoDescriptionMissing;
		if (Icon is not null && (string.IsNullOrWhiteSpace(Icon) || Icon.IndexOfAny(Path.GetInvalidPathChars()) != -1 || Path.IsPathRooted(Icon)))
			yield return OurResources.GameInfoIconInvalid.FormatCurrentCulture(Icon);
	}
}
