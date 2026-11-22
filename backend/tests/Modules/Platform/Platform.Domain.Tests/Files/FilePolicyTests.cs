using Xunit;
using Construction.Platform.Core.Domain.Files;

namespace Construction.Platform.Domain.Tests.Files;

public sealed class FilePolicyTests
{
    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("text/csv", true)]
    [InlineData("application/x-msdownload", false)]
    [InlineData("image/svg+xml", false)]
    [InlineData(null, false)]
    public void ValidateType_uses_the_allow_list(string? contentType, bool allowed)
    {
        Assert.Equal(allowed, FilePolicy.ValidateType(contentType).IsSuccess);
    }

    [Theory]
    [InlineData(FilePolicy.DefaultMaxBytes, true)]
    [InlineData(FilePolicy.DefaultMaxBytes + 1, false)]
    [InlineData(0, false)]
    public void ValidateSize_rejects_over_limit_and_non_positive(long size, bool valid)
    {
        Assert.Equal(valid, FilePolicy.ValidateSize(size, "general").IsSuccess);
    }

    [Fact]
    public void ValidateSize_allows_larger_drawings()
    {
        var result = FilePolicy.ValidateSize(FilePolicy.DrawingMaxBytes, "drawing");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void DetectContentType_recognises_pdf_and_rejects_renamed_executable()
    {
        var pdf = "%PDF-1.7"u8.ToArray();
        var executable = new byte[] { 0x4D, 0x5A, 0x90, 0x00 };

        Assert.Equal("application/pdf", FilePolicy.DetectContentType(pdf));
        Assert.Null(FilePolicy.DetectContentType(executable));
    }

    [Fact]
    public void MagicBytesMatch_rejects_pdf_declared_for_an_executable()
    {
        var detected = FilePolicy.DetectContentType(new byte[] { 0x4D, 0x5A });

        Assert.False(FilePolicy.MagicBytesMatch("application/pdf", detected));
    }

    [Fact]
    public void MagicBytesMatch_accepts_zip_containers_for_docx()
    {
        Assert.True(FilePolicy.MagicBytesMatch(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/zip"));
    }

    [Fact]
    public void StorageKey_contains_only_tenant_year_month_and_file_id()
    {
        var tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var fileId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var key = FilePolicy.StorageKey(
            tenant, fileId, DateTimeOffset.Parse("2026-03-04T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal("t/11111111111111111111111111111111/2026/03/22222222222222222222222222222222", key);
        Assert.DoesNotContain("..", key, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeDownloadName_removes_path_separators_and_control_characters()
    {
        var name = FilePolicy.SanitizeDownloadName("../../invoice\r\nX-Injected: 1.pdf");

        Assert.DoesNotContain("/", name, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", name, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", name, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", name, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(FileStatus.PendingUpload, FileStatus.Uploaded, true)]
    [InlineData(FileStatus.Scanning, FileStatus.Available, true)]
    [InlineData(FileStatus.Scanning, FileStatus.Quarantined, true)]
    [InlineData(FileStatus.Available, FileStatus.Scanning, false)]
    [InlineData(FileStatus.Quarantined, FileStatus.Available, false)]
    public void CanTransition_follows_the_file_state_machine(
        FileStatus from, FileStatus to, bool allowed)
    {
        Assert.Equal(allowed, FilePolicy.CanTransition(from, to));
    }

    [Fact]
    public void IsDownloadable_is_true_only_for_available()
    {
        Assert.True(FilePolicy.IsDownloadable(FileStatus.Available));
        Assert.False(FilePolicy.IsDownloadable(FileStatus.Scanning));
        Assert.False(FilePolicy.IsDownloadable(FileStatus.Quarantined));
    }
}
