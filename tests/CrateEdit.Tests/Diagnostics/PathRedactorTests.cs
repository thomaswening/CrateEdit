using CrateEdit.Core.Diagnostics;

namespace CrateEdit.Tests.Diagnostics;

[Trait("Req", "REQ-118")]
public class PathRedactorTests
{
    [Fact]
    public void Replaces_home_directory_with_tilde()
    {
        // Arrange
        var redactor = new PathRedactor(@"C:\Users\jane", caseSensitive: false);

        // Act
        var redacted = redactor.Redact(@"Failed to write C:\Users\jane\Music\track.mp3");

        // Assert
        Assert.Equal(@"Failed to write ~\Music\track.mp3", redacted);
    }

    [Fact]
    public void Matches_case_insensitively_when_the_filesystem_is_case_insensitive()
    {
        // Arrange
        var redactor = new PathRedactor(@"C:\Users\Jane\", caseSensitive: false);

        // Act
        var redacted = redactor.Redact(@"c:\users\jane\Music");

        // Assert
        Assert.Equal(@"~\Music", redacted);
    }

    [Fact]
    public void Leaves_paths_outside_the_home_directory_unchanged()
    {
        // Arrange
        var redactor = new PathRedactor("/Users/jane", caseSensitive: true);

        // Act
        var redacted = redactor.Redact("/Volumes/USB/track.mp3");

        // Assert
        Assert.Equal("/Volumes/USB/track.mp3", redacted);
    }
}
