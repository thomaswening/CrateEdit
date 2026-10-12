using CrateEdit.Core.Tags;
using CrateEdit.Core.Tags.Writing;

namespace CrateEdit.Tests.Tags;

public sealed class TagReaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("crateedit-formats-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [Trait("Req", "REQ-003")]
    [InlineData("track.mp3", AudioFormat.Mp3)]
    [InlineData("TRACK.MP3", AudioFormat.Mp3)]
    [InlineData("track.flac", null)]
    [InlineData("track", null)]
    public void Format_is_detected_by_extension(string fileName, AudioFormat? expected)
    {
        Assert.Equal(expected, AudioFormats.Detect(fileName));
    }

    [Fact]
    public void Mp3_tags_are_read_through_the_common_reader()
    {
        var path = Path.Combine(_dir, "track.mp3");
        File.WriteAllBytes(path, Mp3Fixture.Build(4, [Frames.Text("TPE1", "Heatwave")]));

        Assert.Equal("Heatwave", TagReader.Read(path).Values[TagField.Artist]);
    }

    [Fact]
    public void Unsupported_format_is_reported_and_never_written()
    {
        var path = Path.Combine(_dir, "track.flac");
        byte[] content = [.. "fLaC"u8, 1, 2, 3];
        File.WriteAllBytes(path, content);

        Assert.Equal(TagProblem.UnsupportedFormat, TagReader.Read(path).WriteProblem?.Problem);
        var result = new SafeFileWriter(new FileRecoveryStore(Path.Combine(_dir, "recovery"))).Write(path, new Dictionary<TagField, string?> { [TagField.Title] = "x" });
        Assert.Equal(TagProblem.UnsupportedFormat, Assert.IsType<TagNotWritable>(result.Error).Problem);
        Assert.Equal(content, File.ReadAllBytes(path));
    }
}
