using System.IO;
using OperaEtDies.Data;

namespace OperaEtDies.Test.Data;

[TestFixture]
public sealed class ImageDataLoaderTests
{
	[Test]
	public void LoadsShippedIcon()
	{
		var gameInfo = GameInfo.Load(JsonDataLoader.DefaultDataDirectory).Value!;
		var iconPath = gameInfo.GetIconPath(JsonDataLoader.DefaultDataDirectory)!;

		var readResult = ImageDataLoader.Read(iconPath);
		Assert.That(readResult.Errors, Is.Empty);

		var decodeResult = ImageDataLoader.Decode(iconPath, readResult.Value!);
		Assert.That(decodeResult.Errors, Is.Empty);
		Assert.That(decodeResult.Value!.Decoder.Frames, Has.Count.GreaterThan(1));
	}

	[Test]
	public void MissingFileReportsError()
	{
		var result = ImageDataLoader.Read(Path.Combine(Path.GetTempPath(), $"OperaEtDiesTest_{Guid.NewGuid():N}.ico"));

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors.Single().Message, Is.EqualTo(OurResources.DataFileNotFound));
	}

	[Test]
	public void InvalidImageReportsError()
	{
		var result = ImageDataLoader.Decode("Icon.ico", [1, 2, 3, 4]);

		Assert.That(result.Succeeded, Is.False);
		Assert.That(result.Errors, Has.Count.EqualTo(1));
	}
}
