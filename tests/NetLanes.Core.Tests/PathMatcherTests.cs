using NetLanes.Core.Utilities;
using Xunit;

namespace NetLanes.Core.Tests;

public class PathMatcherTests
{
    [Theory]
    [InlineData(@"C:\Program Files\Steam\steam.exe", @"c:\program files\steam\steam.exe", true)]
    [InlineData(@"""C:\Program Files\Steam\steam.exe""", @"C:\Program Files\Steam\steam.exe", true)]
    [InlineData(@"C:/Program Files/Steam/steam.exe", @"C:\Program Files\Steam\steam.exe", true)]
    [InlineData(@"\\?\C:\Games\Game.exe", @"c:\games\game.exe", true)]
    [InlineData(@"D:\Games\日本語\Client.exe", @"d:\games\日本語\client.exe", true)]
    [InlineData(@"C:\App\One.exe", @"C:\App\Two.exe", false)]
    public void AreEqual_CorrectlyMatchesWindowsPaths(string path1, string path2, bool expected)
    {
        bool result = PathMatcher.AreEqual(path1, path2);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_HandlesEmptyAndNullGracefully()
    {
        Assert.Equal(string.Empty, PathMatcher.Normalize(null));
        Assert.Equal(string.Empty, PathMatcher.Normalize("   "));
    }

    [Theory]
    [InlineData(@"C:\Games\App.exe", "App.exe")]
    [InlineData(@"""D:\Custom Path\Tool.exe""", "Tool.exe")]
    [InlineData(@"/usr/bin/test", "test")]
    [InlineData(null, "")]
    public void GetFileName_ExtractsBasenameAccurately(string? path, string expected)
    {
        string result = PathMatcher.GetFileName(path);
        Assert.Equal(expected, result);
    }
}
