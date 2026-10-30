using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateHeroBanner;
using TheShop.Application.Features.Homepage.Commands.UpdateHeroBanner;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Homepage.Queries.GetHeroBannerForAdmin;
using TheShop.Application.Features.Homepage.Queries.ValidateHeroBannerImage;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Homepage;

public sealed class HeroBannerHandlerTests
{
    private readonly IHeroBannerRepository _repository = Substitute.For<IHeroBannerRepository>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly Guid _owner = Guid.NewGuid();

    public HeroBannerHandlerTests()
    {
        _user.Id.Returns(_owner);
        _storage.UploadAsync(StorageArea.HeroBannerImages, _owner, Arg.Any<Stream>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("hero/desktop.png");
        _repository.AddAsync(Arg.Any<HeroBanner>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
        _repository.UpdateAsync(Arg.Any<HeroBanner>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
    }

    private static ImageUpload Image() => new([1, 2, 3], "misleading.jpg", "image/png");
    private static HeroBanner Existing() => HeroBanner.Rehydrate("Old", "Description", "Shop", "/old",
        "hero/old.png", "hero/mobile.png", true);
    private static CreateHeroBannerCommand Create() => new("New", null, null, null, Image(), null, true);
    private static UpdateHeroBannerCommand Update(bool removeMobile = false) =>
        new("Changed", null, null, null, null, null, false, removeMobile, false);

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Create_AbsentBanner_UploadsCanonicalPngAndPersistsSingleton()
    {
        var result = await new CreateHeroBannerHandler(_repository, _storage, _user).Handle(Create(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        await _storage.Received(1).UploadAsync(StorageArea.HeroBannerImages, _owner, Arg.Any<Stream>(),
            "image.png", "image/png", Arg.Any<CancellationToken>());
        await _repository.Received(1).AddAsync(Arg.Is<HeroBanner>(b => b.Heading == "New" &&
            b.DesktopImageKey == "hero/desktop.png" && b.Id == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Create_ExistingHiddenBanner_ReturnsConflictWithoutUploading()
    {
        var existing = Existing();
        existing.SetVisibility(false);
        _repository.GetForWriteAsync(true, Arg.Any<CancellationToken>()).Returns(existing);
        var result = await new CreateHeroBannerHandler(_repository, _storage, _user).Handle(Create(), CancellationToken.None);
        result.Error.Should().Be(HeroBannerErrorKeys.AlreadyExists);
        await _storage.DidNotReceiveWithAnyArgs().UploadAsync(default, default, default!, default!, default!, default);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Update_MissingBanner_ReturnsNotFoundWithoutWriting()
    {
        var result = await new UpdateHeroBannerHandler(_repository, _storage, _user).Handle(Update(), CancellationToken.None);
        result.Error.Should().Be(HeroBannerErrorKeys.NotFound);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Update_RemoveMobile_PreservesDesktopAndSavesHidden()
    {
        _repository.GetForWriteAsync(false, Arg.Any<CancellationToken>()).Returns(Existing());
        var result = await new UpdateHeroBannerHandler(_repository, _storage, _user)
            .Handle(Update(removeMobile: true), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(Arg.Is<HeroBanner>(b => b.Heading == "Changed" &&
            b.DesktopImageKey == "hero/old.png" && b.MobileImageKey == null && !b.IsVisible),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Create_UploadFailure_DoesNotPersistConfiguration()
    {
        _storage.UploadAsync(StorageArea.HeroBannerImages, _owner, Arg.Any<Stream>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException());
        var result = await new CreateHeroBannerHandler(_repository, _storage, _user).Handle(Create(), CancellationToken.None);
        result.Error.Should().Be(HeroBannerErrorKeys.UploadFailed);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Create_DefinitePersistenceFailure_RemovesOnlyNewUpload()
    {
        _repository.AddAsync(Arg.Any<HeroBanner>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail(HeroBannerErrorKeys.AlreadyExists));
        var result = await new CreateHeroBannerHandler(_repository, _storage, _user).Handle(Create(), CancellationToken.None);
        result.Error.Should().Be(HeroBannerErrorKeys.AlreadyExists);
        await _storage.Received(1).DeleteAsync(StorageArea.HeroBannerImages, "hero/desktop.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Create_UnconfirmedPersistence_KeepsUploadForReconciliation()
    {
        _repository.AddAsync(Arg.Any<HeroBanner>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException());
        var result = await new CreateHeroBannerHandler(_repository, _storage, _user).Handle(Create(), CancellationToken.None);
        result.Error.Should().Be(HeroBannerErrorKeys.SaveUnconfirmed);
        await _storage.DidNotReceiveWithAnyArgs().DeleteAsync(default, default!, default);
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Read_AbsentBanner_ReturnsSuccessfulNullWithoutSignedUrl()
    {
        var result = await new GetHeroBannerForAdminHandler(_repository, _storage)
            .Handle(new GetHeroBannerForAdminQuery(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        await _storage.DidNotReceiveWithAnyArgs().CreateSignedUrlAsync(default, default!, default, default);
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Read_ExistingBanner_SignsBothPrivatePreviews()
    {
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(Existing());
        _storage.CreateSignedUrlAsync(StorageArea.HeroBannerImages, Arg.Any<string>(), 900,
            Arg.Any<CancellationToken>()).Returns(call => "signed:" + call.ArgAt<string>(1));
        var result = await new GetHeroBannerForAdminHandler(_repository, _storage)
            .Handle(new GetHeroBannerForAdminQuery(), CancellationToken.None);
        result.Value!.DesktopImage.PreviewUrl.Should().Be("signed:hero/old.png");
        result.Value.MobileImage!.PreviewUrl.Should().Be("signed:hero/mobile.png");
        await _storage.Received(2).CreateSignedUrlAsync(StorageArea.HeroBannerImages, Arg.Any<string>(), 900,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task SelectionHandler_AfterValidation_ReturnsSuccess()
    {
        var result = await new ValidateHeroBannerImageHandler()
            .Handle(new(TheShop.Application.Features.Homepage.DTOs.HeroBannerImageRole.Desktop, Image()), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
    }
}
