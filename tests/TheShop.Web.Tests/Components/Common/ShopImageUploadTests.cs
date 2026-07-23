using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Web.Components.Common;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

/// <summary>
/// Tests for <see cref="ShopImageUpload"/> — the reusable dropzone-with-preview control backing
/// the add-brand logo field (Behavior 2, Figma node <c>2490:1257</c>/<c>2493:6632</c>). Covers
/// preview rendering, removal (returning the form to its no-logo state), and the client-side
/// type/size guard that backs RULE-4/AC-5.
/// <see href=".specs/add-brand/spec.md"/>
/// </summary>
public class ShopImageUploadTests : TestContext
{
    private const string InvalidTypeError = "Logo must be a PNG, JPG, or WebP image.";
    private const string TooLargeError = "Logo must be 2 MB or smaller.";

    public ShopImageUploadTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

    private static ShopUploadedImage ExampleImage(string fileName = "logo.png") =>
        new([1, 2, 3], fileName, "image/png", "data:image/png;base64,AQID");

    // =========================================================================
    // Preview rendering (Behavior 2, AC-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Render_WithASelectedImage_ShowsAPreview()
    {
        var cut = Render<ShopImageUpload>(p => p.Add(c => c.Files, [ExampleImage()]));

        cut.FindAll("img").Should().Contain(img => img.GetAttribute("src") == "data:image/png;base64,AQID");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Render_WithNoSelectedImage_ShowsNoPreview()
    {
        var cut = Render<ShopImageUpload>(p => p.Add(c => c.Files, []));

        cut.FindAll("img").Should().BeEmpty();
    }

    // =========================================================================
    // Remove — the form returns to its no-logo state (Behavior 2 edge case)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task ClickRemove_WithASingleSelectedImage_RaisesFilesChangedWithAnEmptyList()
    {
        IReadOnlyList<ShopUploadedImage>? raised = null;
        var cut = Render<ShopImageUpload>(p => p
            .Add(c => c.Files, [ExampleImage()])
            .Add(c => c.FilesChanged, images => raised = images));

        await cut.Find("button.remove").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        raised.Should().NotBeNull();
        raised!.Should().BeEmpty();
    }

    // =========================================================================
    // Type guard (RULE-4, AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void SelectFile_WithADisallowedContentType_DoesNotAcceptItAndShowsTheTypeError()
    {
        IReadOnlyList<ShopUploadedImage>? raised = null;
        var cut = Render<ShopImageUpload>(p => p
            .Add(c => c.Files, [])
            .Add(c => c.FilesChanged, images => raised = images)
            .Add(c => c.InvalidTypeError, InvalidTypeError));

        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary([1, 2, 3], "logo.gif", contentType: "image/gif"));

        raised.Should().NotBeNull();
        raised!.Should().BeEmpty("a disallowed content type must not be accepted into the selection");
        cut.Markup.Should().Contain(InvalidTypeError);
    }

    // =========================================================================
    // Size guard (RULE-4, AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void SelectFile_ExceedingTheMaxFileSize_DoesNotAcceptItAndShowsTheSizeError()
    {
        IReadOnlyList<ShopUploadedImage>? raised = null;
        var oversized = new byte[3 * 1024 * 1024];
        var cut = Render<ShopImageUpload>(p => p
            .Add(c => c.Files, [])
            .Add(c => c.FilesChanged, images => raised = images)
            .Add(c => c.MaxFileSizeError, TooLargeError)
            .Add(c => c.MaxFileSize, 2 * 1024 * 1024));

        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary(oversized, "logo.png", contentType: "image/png"));

        raised.Should().NotBeNull();
        raised!.Should().BeEmpty("an oversized file must not be accepted into the selection");
        cut.Markup.Should().Contain(TooLargeError);
    }

    // =========================================================================
    // Valid selection — accepted into the file list (AC-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void SelectFile_WithAnAllowedTypeAndSize_RaisesFilesChangedWithOneImage()
    {
        IReadOnlyList<ShopUploadedImage>? raised = null;
        var cut = Render<ShopImageUpload>(p => p
            .Add(c => c.Files, [])
            .Add(c => c.FilesChanged, images => raised = images));

        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary([1, 2, 3], "logo.png", contentType: "image/png"));

        raised.Should().NotBeNull();
        raised!.Should().ContainSingle(image => image.FileName == "logo.png" && image.ContentType == "image/png");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-4: Render_WithASelectedImage_ShowsAPreview, SelectFile_WithAnAllowedTypeAndSize_RaisesFilesChangedWithOneImage
// AC-5: SelectFile_WithADisallowedContentType_DoesNotAcceptItAndShowsTheTypeError,
//        SelectFile_ExceedingTheMaxFileSize_DoesNotAcceptItAndShowsTheSizeError
// (Behavior 2 remove edge case: ClickRemove_WithASingleSelectedImage_RaisesFilesChangedWithAnEmptyList,
//  Render_WithNoSelectedImage_ShowsNoPreview)
