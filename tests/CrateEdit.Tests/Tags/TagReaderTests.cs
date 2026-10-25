using CrateEdit.Core.Tags;
using CrateEdit.Core.Tags.Writing;

namespace CrateEdit.Tests.Tags;

public sealed class TagReaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("crateedit-formats-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteFile(string name, byte[] content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static byte[] FlacFile() => [.. "fLaC"u8, 1, 2, 3];

    [Theory]
    [Trait("Req", "REQ-003")]
    [InlineData("track.mp3", AudioFormat.Mp3)]
    [InlineData("TRACK.MP3", AudioFormat.Mp3)]
    [InlineData("track.wav", AudioFormat.Wav)]
    [InlineData("track.flac", null)]
    [InlineData("track", null)]
    public void Format_is_detected_by_extension(string fileName, AudioFormat? expected)
    {
        // Act
        var format = AudioFormats.Detect(fileName);

        // Assert
        Assert.Equal(expected, format);
    }

    [Fact]
    public void Mp3_tags_are_read_through_the_common_reader()
    {
        // Arrange
        var path = WriteFile("track.mp3", Mp3Fixture.Build(4, [Frames.Text("TPE1", "Heatwave")]));

        // Act
        var tags = TagReader.Read(path);

        // Assert
        Assert.Equal("Heatwave", tags.Values[TagField.Artist]);
    }

    [Fact]
    public void Unsupported_format_is_reported_as_not_writable()
    {
        // Arrange
        var path = WriteFile("track.flac", FlacFile());

        // Act
        var tags = TagReader.Read(path);

        // Assert
        Assert.Equal(TagProblem.UnsupportedFormat, tags.WriteProblem?.Problem);
    }

    [Fact]
    public void Unsupported_format_is_never_written()
    {
        // Arrange
        var path = WriteFile("track.flac", FlacFile());
        var writer = new SafeFileWriter(new FileRecoveryStore(Path.Combine(_dir, "recovery")));
        var changes = new Dictionary<TagField, string?> { [TagField.Title] = "New" };

        // Act
        var result = writer.Write(path, changes);

        // Assert
        Assert.Equal(TagProblem.UnsupportedFormat, Assert.IsType<TagNotWritable>(result.Error).Problem);
        Assert.Equal(FlacFile(), File.ReadAllBytes(path));
    }
}
