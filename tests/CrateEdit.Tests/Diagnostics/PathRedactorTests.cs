using CrateEdit.Core.Diagnostics;

namespace CrateEdit.Tests.Diagnostics;

[Trait("Req", "REQ-118")]
public class PathRedactorTests
{
    [Fact]
    public void Replaces_home_directory_with_tilde()
    {
        var redactor = new PathRedactor(@"C:\Users\jane", caseSensitive: false);

        Assert.Equal(@"Failed to write ~\Music\track.mp3",
            redactor.Redact(@"Failed to write C:\Users\jane\Music\track.mp3"));
    }

    [Fact]
    public void Matches_case_insensitively_when_the_filesystem_is_case_insensitive()
    {
        var redactor = new PathRedactor(@"C:\Users\Jane\", caseSensitive: false);

        Assert.Equal(@"~\Music", redactor.Redact(@"c:\users\jane\Music"));
    }

    [Fact]
    public void Leaves_paths_outside_the_home_directory_unchanged()
    {
        var redactor = new PathRedactor("/Users/jane", caseSensitive: true);

        Assert.Equal("/Volumes/USB/track.mp3", redactor.Redact("/Volumes/USB/track.mp3"));
    }
}
