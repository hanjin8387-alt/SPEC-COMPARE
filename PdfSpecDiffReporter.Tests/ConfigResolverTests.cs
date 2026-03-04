using System;
using System.IO;
using PdfSpecDiffReporter.Helpers;
using PdfSpecDiffReporter.Models;

namespace PdfSpecDiffReporter.Tests;

public sealed class ConfigResolverTests
{
    [Fact]
    public void Load_WhenConfigPathIsNull_ReturnsDefaultConfig()
    {
        var result = PipelineConfigResolver.Load(null);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Config);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Load_AllowsCommentsAndTrailingCommas()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(
                path,
                """
                {
                  // comments should be ignored
                  "diffThreshold": 0.9,
                  "chapterMatchThreshold": 0.6,
                }
                """);

            var result = PipelineConfigResolver.Load(path);

            Assert.True(result.IsValid);
            Assert.NotNull(result.Config);
            Assert.Equal(0.9d, result.Config!.DiffThreshold);
            Assert.Equal(0.6d, result.Config.ChapterMatchThreshold);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_WhenJsonIsInvalid_ReturnsValidationFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, "{ \"diffThreshold\": }");
            var result = PipelineConfigResolver.Load(path);

            Assert.False(result.IsValid);
            Assert.False(result.IsIoRelated);
            Assert.Equal("Config JSON is invalid.", result.ErrorMessage);
            Assert.Null(result.Config);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Resolve_PrioritizesOverridesThenConfigThenDefaults()
    {
        var config = new PipelineConfig
        {
            DiffThreshold = 0.6d,
            ChapterMatchThreshold = 0.5d
        };

        var options = PipelineConfigResolver.Resolve(
            config,
            diffThresholdOverride: 0.92d,
            chapterMatchThresholdOverride: null,
            defaultDiffThreshold: 0.85d,
            defaultChapterMatchThreshold: 0.70d);

        Assert.Equal(0.92d, options.DiffThreshold);
        Assert.Equal(0.5d, options.ChapterMatchThreshold);
    }

    [Fact]
    public void Resolve_UsesDefaultsWhenConfigAndOverridesAreMissing()
    {
        var options = PipelineConfigResolver.Resolve(
            config: null,
            diffThresholdOverride: null,
            chapterMatchThresholdOverride: null,
            defaultDiffThreshold: 0.85d,
            defaultChapterMatchThreshold: 0.70d);

        Assert.Equal(0.85d, options.DiffThreshold);
        Assert.Equal(0.70d, options.ChapterMatchThreshold);
        Assert.NotNull(options.TextNormalization);
        Assert.NotNull(options.ChapterSegmentation);
    }
}
