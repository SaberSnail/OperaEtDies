using System.IO;

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

	public IEnumerable<string> Validate()
	{
		if (string.IsNullOrWhiteSpace(Title))
			yield return OurResources.GameInfoTitleMissing;
		if (string.IsNullOrWhiteSpace(Description))
			yield return OurResources.GameInfoDescriptionMissing;
	}
}
