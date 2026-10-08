using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class NativeImageUploadTests : TestContext
{
    private readonly BunitJSModuleInterop _module;
    public NativeImageUploadTests()
    {
        Services.AddSingleton<BusyState>();
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule("./js/shopImageUpload.js");
        _module.Setup<string>("createObjectUrl", _ => true).SetResult("blob:owned");
    }

    private static ShopUploadedImage Image(string name) => new([1, 2], name, "image/png", "https://example.test/" + name);

    private IRenderedComponent<ShopImageUpload> Bound(params ShopUploadedImage[] images)
    {
        IRenderedComponent<ShopImageUpload>? cut = null;
        cut = Render<ShopImageUpload>(p => p.Add(c => c.Multiple, true).Add(c => c.ShowPrimaryBadge, true)
            .Add(c => c.Files, images).Add(c => c.FilesChanged, value => cut!.Render(p => p.Add(c => c.Files, value))));
        return cut;
    }

    [Fact]
    public async Task Reorder_PreservesIdentityAndBytes_AndPromotesFirstImage()
    {
        var first = Image("first.png");
        var second = Image("second.png");
        var cut = Bound(first, second);
        await cut.InvokeAsync(() => cut.Instance.ReorderAsync(second.ClientId.ToString(), first.ClientId.ToString()));
        cut.Instance.Files.Should().Equal(second, first);
        cut.Instance.Files[0].Bytes.Should().BeSameAs(second.Bytes);
        cut.FindAll(".shop-image-tile-surface")[0].GetAttribute("aria-label").Should().Be("second.png");
        cut.FindComponents<ShopImageTile>()[0].Instance.Primary.Should().BeTrue();
        cut.Find(".shop-image-upload > span[role=status]").TextContent.Should().Contain("second.png");
    }

    [Fact]
    public async Task KeyboardMove_AndRemove_PreserveOrderAndReturnToDropzone()
    {
        var cut = Bound(Image("first.png"), Image("second.png"));
        await cut.FindAll(".shop-image-tile-surface")[1].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft", AltKey = true });
        cut.Instance.Files[0].FileName.Should().Be("second.png");
        await cut.FindAll(".shop-image-tile-remove")[0].ClickAsync(new());
        cut.Instance.Files.Should().ContainSingle(i => i.FileName == "first.png");
        await cut.Find(".shop-image-tile-remove").ClickAsync(new());
        cut.Instance.Files.Should().BeEmpty();
        cut.Find(".shop-file-dropzone-trigger").Should().NotBeNull();
    }

    [Fact]
    public async Task Disabled_BlocksProgrammaticRemovalReorderAndSelection()
    {
        var first = Image("first.png"); var second = Image("second.png");
        var cut = Bound(first, second);
        cut.Render(p => p.Add(c => c.Disabled, true));
        await cut.InvokeAsync(() => cut.Instance.ReorderAsync(second.ClientId.ToString(), first.ClientId.ToString()));
        await cut.FindComponent<ShopImageTile>().InvokeAsync(() => cut.FindComponent<ShopImageTile>().Instance.OnRemove.InvokeAsync());
        var file = Substitute.For<IBrowserFile>();
        await cut.InvokeAsync(() => cut.FindComponent<ShopFileDropzone>().Instance.FilesSelected.InvokeAsync(new[] { file }));
        cut.Instance.Files.Should().Equal(first, second);
        file.DidNotReceiveWithAnyArgs().OpenReadStream(default, Xunit.TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("image/png", 3000000)]
    [InlineData("text/plain", 10)]
    public async Task RejectedFile_IsVisibleAndNeverRead(string type, long size)
    {
        var file = Substitute.For<IBrowserFile>();
        file.Name.Returns("invalid.png"); file.ContentType.Returns(type); file.Size.Returns(size);
        var cut = Bound();
        await cut.InvokeAsync(() => cut.FindComponent<ShopFileDropzone>().Instance.FilesSelected.InvokeAsync(new[] { file }));
        file.DidNotReceiveWithAnyArgs().OpenReadStream(default, Xunit.TestContext.Current.CancellationToken);
        cut.Instance.Files.Should().ContainSingle(f => f.Error != null && f.Bytes.Length == 0);
        cut.Find(".shop-image-tile-error").Should().NotBeNull();
    }

    [Fact]
    public async Task ReadFailure_ShowsErrorAndReleasesBusyState()
    {
        var file = Substitute.For<IBrowserFile>();
        file.Name.Returns("broken.png"); file.ContentType.Returns("image/png"); file.Size.Returns(10);
        file.OpenReadStream(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(_ => throw new IOException());
        var cut = Bound();
        await cut.InvokeAsync(() => cut.FindComponent<ShopFileDropzone>().Instance.FilesSelected.InvokeAsync(new[] { file }));
        cut.Instance.Files.Single().Error.Should().Be(Strings.ImageUpload_ReadError);
        Services.GetRequiredService<BusyState>().IsAnyBusy.Should().BeFalse();
    }

    [Fact]
    public void Selection_AtLimit_ShowsCountErrorAndKeepsExistingImage()
    {
        var cut = Bound(Image("first.png"));
        cut.Render(p => p.Add(c => c.MaxFileCount, 1));
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary([1], "extra.png", contentType: "image/png"));
        cut.Instance.Files.Should().ContainSingle(i => i.FileName == "first.png");
        cut.Find(".shop-image-upload-error").TextContent.Should().Contain("1");
    }

    [Fact]
    public async Task SingleReplacement_ReleasesOwnedPreview_ButNotPersistedUrl()
    {
        var cut = Bound(ShopUploadedImage.Existing(Guid.NewGuid(), "https://example.test/stored.png"));
        cut.Render(p => p.Add(c => c.Multiple, false));
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary([1, 2], "one.png", contentType: "image/png"));
        cut.Instance.Files.Single().PreviewUrl.Should().Be("blob:owned");
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary([1], "invalid.txt", contentType: "text/plain"));
        cut.Instance.Files.Single().Error.Should().NotBeNull();
        var calls = _module.Invocations.Where(i => i.Identifier == "revokeObjectUrl").ToArray();
        calls.Should().ContainSingle(i => (string)i.Arguments[0]! == "blob:owned");
        await cut.InvokeAsync(() => cut.Instance.WaitForPendingFilesAsync());
    }

    [Fact]
    public async Task PendingConsumer_IsAwaitedBeforeInputReset_AndBlocksSecondPick()
    {
        var release = new TaskCompletionSource();
        var calls = 0;
        var cut = Render<ShopFileDropzone>(p => p.Add(c => c.FilesSelected, async _ => { calls++; await release.Task; }));
        var input = cut.FindComponent<InputFile>();
        var batch = new InputFileChangeEventArgs([Substitute.For<IBrowserFile>()]);
        var first = cut.InvokeAsync(() => input.Instance.OnChange.InvokeAsync(batch));
        await cut.InvokeAsync(() => input.Instance.OnChange.InvokeAsync(batch));
        calls.Should().Be(1);
        first.IsCompleted.Should().BeFalse();
        release.SetResult();
        await first;
    }

    [Fact]
    public async Task Preparation_ShowsOnlyLoader_BlocksEdits_AndSaveWaits()
    {
        var release = new TaskCompletionSource();
        var cut = Bound(Image("stored.png"));
        var selection = cut.InvokeAsync(() => cut.FindComponent<ShopFileDropzone>().Instance.FilesSelected.InvokeAsync(new[] { DelayedFile(release.Task) }));
        cut.WaitForAssertion(() => cut.FindAll(".shop-image-tile-loader").Should().ContainSingle());
        cut.FindComponents<ShopImageTile>().Should().OnlyContain(tile => tile.Instance.Disabled);
        cut.FindComponents<ShopImageTile>().Last().FindAll(".shop-image").Should().BeEmpty();
        cut.Instance.WaitForPendingFilesAsync().IsCompleted.Should().BeFalse();
        release.SetResult();
        await selection;
        await cut.Instance.WaitForPendingFilesAsync();
        cut.Instance.Files.Should().HaveCount(2);
        cut.FindAll(".shop-image-tile-loader").Should().BeEmpty();
    }

    [Fact]
    public async Task ExternalReset_DuringPreparation_WinsAndReleasesNewPreview()
    {
        var release = new TaskCompletionSource();
        var cut = Bound(Image("stored.png"));
        var selection = cut.InvokeAsync(() => cut.FindComponent<ShopFileDropzone>().Instance.FilesSelected.InvokeAsync(new[] { DelayedFile(release.Task) }));
        cut.WaitForAssertion(() => cut.FindAll(".shop-image-tile-loader").Should().ContainSingle());
        cut.Render(p => p.Add(c => c.Files, Array.Empty<ShopUploadedImage>()));
        release.SetResult();
        await selection;
        cut.Instance.Files.Should().BeEmpty();
        _module.Invocations.Should().Contain(i => i.Identifier == "revokeObjectUrl" && (string)i.Arguments[0]! == "blob:owned");
    }

    [Fact]
    public async Task ExternalReset_DuringFailedRead_ReleasesEarlierOwnedPreview()
    {
        var release = new TaskCompletionSource();
        var cut = Bound();
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary([1], "earlier.png", contentType: "image/png"));
        var selection = cut.InvokeAsync(() => cut.FindComponent<ShopFileDropzone>().Instance.FilesSelected.InvokeAsync(new[] { DelayedFile(release.Task, fail: true) }));
        cut.WaitForAssertion(() => cut.FindAll(".shop-image-tile-loader").Should().ContainSingle());
        cut.Render(p => p.Add(c => c.Files, Array.Empty<ShopUploadedImage>()));
        release.SetResult();
        await selection;
        cut.Instance.Files.Should().BeEmpty();
        _module.Invocations.Where(i => i.Identifier == "revokeObjectUrl").Should().ContainSingle(i => (string)i.Arguments[0]! == "blob:owned");
    }

    [Fact]
    public async Task Disposal_CancelsPreparation_WithoutPublishingPartialSelection()
    {
        var cut = Bound(Image("stored.png"));
        var selection = cut.InvokeAsync(() => cut.FindComponent<ShopFileDropzone>().Instance.FilesSelected.InvokeAsync(new[] { DelayedFile(new TaskCompletionSource().Task) }));
        cut.WaitForAssertion(() => cut.FindAll(".shop-image-tile-loader").Should().ContainSingle());
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await selection;
        cut.Instance.Files.Should().ContainSingle(f => f.FileName == "stored.png");
        Services.GetRequiredService<BusyState>().IsAnyBusy.Should().BeFalse();
        _module.Invocations.Should().NotContain(i => i.Identifier == "createObjectUrl");
    }

    private static IBrowserFile DelayedFile(Task release, bool fail = false)
    {
        var file = Substitute.For<IBrowserFile>();
        file.Name.Returns("pending.png"); file.ContentType.Returns("image/png"); file.Size.Returns(2);
        file.OpenReadStream(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(new GatedStream(release, fail));
        return file;
    }

    private sealed class GatedStream(Task release, bool fail) : MemoryStream([1, 2])
    {
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await release.WaitAsync(cancellationToken);
            if (fail) throw new IOException();
            await base.CopyToAsync(destination, bufferSize, cancellationToken);
        }
    }

    [Fact]
    public void Tile_PickerModeHasNoRemoval_IncludingErrorState()
    {
        var cut = Render<ShopImageTile>(p => p.Add(c => c.Alt, "Stored image").Add(c => c.Removable, false)
            .Add(c => c.Selected, true).Add(c => c.ErrorText, "Unavailable").Add(c => c.OnClick, () => { }));
        cut.FindAll(".shop-image-tile-remove").Should().BeEmpty();
        cut.Find(".shop-image-tile-surface").GetAttribute("aria-pressed").Should().Be("true");
        cut.FindAll("button button").Should().BeEmpty();
    }
}
