using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Web.Common;
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
    private const string PrimaryLabel = "Primary";

    public ShopImageUploadTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton<BusyState>();
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
    // Type guard (RULE-4, AC-5) — the row still appears, flagged with its own error (Figma)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void SelectFile_WithADisallowedContentType_KeepsItAsARowFlaggedWithTheTypeError()
    {
        IReadOnlyList<ShopUploadedImage>? raised = null;
        var cut = Render<ShopImageUpload>(p => p
            .Add(c => c.Files, [])
            .Add(c => c.FilesChanged, images => raised = images)
            .Add(c => c.InvalidTypeError, InvalidTypeError));

        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary([1, 2, 3], "logo.gif", contentType: "image/gif"));

        raised.Should().NotBeNull();
        raised!.Should().ContainSingle(image => image.Error == InvalidTypeError,
            "a disallowed content type stays in the selection as an error row, not silently dropped");

        // Echo the callback back through Files, exactly as a consumer's @bind-Files would.
        cut.Render(p => p.Add(c => c.Files, raised));
        cut.Markup.Should().Contain(InvalidTypeError);
    }

    // =========================================================================
    // Size guard (RULE-4, AC-5) — the row still appears, flagged with its own error (Figma)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void SelectFile_ExceedingTheMaxFileSize_KeepsItAsARowFlaggedWithTheSizeError()
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
        raised!.Should().ContainSingle(image => image.Error == TooLargeError,
            "an oversized file stays in the selection as an error row, not silently dropped");

        // Echo the callback back through Files, exactly as a consumer's @bind-Files would.
        cut.Render(p => p.Add(c => c.Files, raised));
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

    // =========================================================================
    // Primary badge — position, not a flag, is what makes an image primary (FR-11)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Render_WithThePrimaryBadgeEnabled_BadgesOnlyTheFirstImage()
    {
        var cut = Render<ShopImageUpload>(p => p
            .Add(c => c.Files, [ExampleImage("first.png"), ExampleImage("second.png")])
            .Add(c => c.Multiple, true)
            .Add(c => c.ShowPrimaryBadge, true)
            .Add(c => c.PrimaryLabel, PrimaryLabel));

        cut.FindAll(".preview .primary").Should().ContainSingle();
        cut.Find(".preview .primary").TextContent.Should().Contain(PrimaryLabel);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Render_WithThePrimaryBadgeDisabled_BadgesNothing()
    {
        var cut = Render<ShopImageUpload>(p => p
            .Add(c => c.Files, [ExampleImage("first.png"), ExampleImage("second.png")])
            .Add(c => c.Multiple, true)
            .Add(c => c.PrimaryLabel, PrimaryLabel));

        cut.FindAll(".preview .primary").Should().BeEmpty();
    }

    // =========================================================================
    // Drag-to-reorder — the dropped image lands at its new index (FR-11)
    // =========================================================================

}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-4: Render_WithASelectedImage_ShowsAPreview, SelectFile_WithAnAllowedTypeAndSize_RaisesFilesChangedWithOneImage
// AC-5: SelectFile_WithADisallowedContentType_KeepsItAsARowFlaggedWithTheTypeError,
//        SelectFile_ExceedingTheMaxFileSize_KeepsItAsARowFlaggedWithTheSizeError
// (Behavior 2 remove edge case: ClickRemove_WithASingleSelectedImage_RaisesFilesChangedWithAnEmptyList,
//  Render_WithNoSelectedImage_ShowsNoPreview)
// FR-11 (create-product, gallery sequencing): Render_WithThePrimaryBadgeEnabled_BadgesOnlyTheFirstImage,
//        Render_WithThePrimaryBadgeDisabled_BadgesNothing
// (Drag-to-reorder was dropped from the component — selection order alone decides the primary
//  image, so the three drop tests that covered AllowReorder went with it.)
