using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TheShop.Application.Common.Behaviors;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Homepage;
using TheShop.Application.Features.Homepage.Commands.CreateFeatured;
using TheShop.Application.Features.Homepage.Commands.UpdateFeatured;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;
using TheShop.Application.Features.Homepage.Queries.GetFeaturedForAdmin;
using TheShop.Application.Features.Homepage.Queries.ValidateFeaturedImage;
using TheShop.Application.Features.Roles;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Application.Tests.Features.Homepage;

public sealed class FeaturedHandlerTests
{
    private readonly IFeaturedRepository _repository = Substitute.For<IFeaturedRepository>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly Guid _owner = Guid.NewGuid();
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static ImageUpload Image(string mime = "image/png") => new([1, 2, 3], "wrong.extension", mime);
    private static FeaturedTileInput Tile(string title, ImageUpload? image = null, bool remove = false) =>
        new(null, title, null, null, image, remove);
    private static FeaturedSection Existing() => FeaturedSection.Rehydrate("Saved",
        FeaturedTile.Create("Label", "One", "Shop", "/old", "old-one"),
        FeaturedTile.Create(null, "Two", null, null, "old-two"), false);
    private static CreateFeaturedCommand Create() => new("Summer", Tile("First", Image()), Tile("Second", Image("image/webp")), true);
    private static UpdateFeaturedCommand Update() => new("Updated", Tile("Changed One"), Tile("Changed Two"), true);

    public FeaturedHandlerTests()
    {
        _user.Id.Returns(_owner);
        _repository.AddAsync(Arg.Any<FeaturedSection>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
        _repository.UpdateAsync(Arg.Any<FeaturedSection>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
        _storage.UploadAsync(StorageArea.FeaturedImages, _owner, Arg.Any<Stream>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => "new-" + call.ArgAt<string>(3));
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Create_UploadsBothCanonicalImagesBeforeOneCompleteCommit()
    {
        var bytes = new List<byte[]>();
        _storage.UploadAsync(StorageArea.FeaturedImages, _owner, Arg.Any<Stream>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            using var copy = new MemoryStream();
            call.ArgAt<Stream>(2).CopyTo(copy);
            bytes.Add(copy.ToArray());
            return "new-" + call.ArgAt<string>(3);
        });
        _repository.AddAsync(Arg.Any<FeaturedSection>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            bytes.Should().HaveCount(2);
            return Result.Ok();
        });
        var result = await new CreateFeaturedHandler(_repository, _storage, _user).Handle(Create(), Token);
        result.IsSuccess.Should().BeTrue();
        bytes.Should().OnlyContain(b => b.SequenceEqual(new byte[] { 1, 2, 3 }));
        await _storage.Received(1).UploadAsync(StorageArea.FeaturedImages, _owner, Arg.Any<Stream>(), "image.png", "image/png", Token);
        await _storage.Received(1).UploadAsync(StorageArea.FeaturedImages, _owner, Arg.Any<Stream>(), "image.webp", "image/webp", Token);
        await _repository.Received(1).AddAsync(Arg.Is<FeaturedSection>(x => x.Id == 1 && x.Title == "Summer" &&
            x.Tile1.Title == "First" && x.Tile1.ImageKey == "new-image.png" && x.Tile2.Title == "Second" &&
            x.Tile2.ImageKey == "new-image.webp" && x.IsVisible), Token);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Create_ExistingHiddenSectionRejectsDuplicateBeforeUpload()
    {
        _repository.GetAsync(Token).Returns(Existing());
        var result = await new CreateFeaturedHandler(_repository, _storage, _user).Handle(Create(), Token);
        result.Error.Should().Be(FeaturedErrorKeys.AlreadyExists);
        await _storage.DidNotReceiveWithAnyArgs().UploadAsync(default, default, default!, default!, default!, default);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Update_MissingSectionRejectsBeforeWriting()
    {
        var result = await new UpdateFeaturedHandler(_repository, _storage, _user).Handle(Update(), Token);
        result.Error.Should().Be(FeaturedErrorKeys.NotFound);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Update_OnlyChangedImageUploadsAndSavedAggregateRemainsUntouchedUntilCommit()
    {
        var saved = Existing();
        _repository.GetAsync(Token).Returns(saved);
        var command = Update() with { Tile2 = Tile("Changed Two", Image("image/jpeg"), true), IsVisible = false };
        (await new UpdateFeaturedHandler(_repository, _storage, _user).Handle(command, Token)).IsSuccess.Should().BeTrue();
        await _storage.Received(1).UploadAsync(StorageArea.FeaturedImages, _owner, Arg.Any<Stream>(), "image.jpg", "image/jpeg", Token);
        await _repository.Received(1).UpdateAsync(Arg.Is<FeaturedSection>(x => x.Title == "Updated" &&
            x.Tile1.ImageKey == "old-one" && x.Tile2.ImageKey == "new-image.jpg" && !x.IsVisible), Token);
        saved.Title.Should().Be("Saved");
        saved.Tile2.ImageKey.Should().Be("old-two");
        await _storage.DidNotReceiveWithAnyArgs().DeleteAsync(default, default!, default);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Update_SecondUploadFailureLeavesSavedRowAndRetainsFirstUpload()
    {
        var saved = Existing();
        _repository.GetAsync(Token).Returns(saved);
        _storage.UploadAsync(StorageArea.FeaturedImages, _owner, Arg.Any<Stream>(), "image.webp", "image/webp", Token)
            .ThrowsAsync(new IOException("second upload failed"));
        var command = new UpdateFeaturedCommand("Draft", Tile("First", Image()), Tile("Second", Image("image/webp")), true);
        var result = await new UpdateFeaturedHandler(_repository, _storage, _user).Handle(command, Token);
        result.Error.Should().Be(FeaturedErrorKeys.UploadFailed);
        await _storage.Received(1).UploadAsync(StorageArea.FeaturedImages, _owner, Arg.Any<Stream>(), "image.png", "image/png", Token);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await _storage.DidNotReceiveWithAnyArgs().DeleteAsync(default, default!, default);
        saved.Title.Should().Be("Saved");
        saved.Tile1.ImageKey.Should().Be("old-one");
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Create_DefiniteAndUncertainSaveFailuresNeverDeleteUploads()
    {
        foreach (var error in new[] { FeaturedErrorKeys.AlreadyExists, RbacErrorKeys.AccessDenied, FeaturedErrorKeys.SaveFailed })
        {
            _repository.AddAsync(Arg.Any<FeaturedSection>(), Token).Returns(Result.Fail(error));
            (await new CreateFeaturedHandler(_repository, _storage, _user).Handle(Create(), Token)).Error.Should().Be(error);
        }
        _repository.AddAsync(Arg.Any<FeaturedSection>(), Token).ThrowsAsync(new IOException("commit result lost"));
        (await new CreateFeaturedHandler(_repository, _storage, _user).Handle(Create(), Token)).Error.Should().Be(FeaturedErrorKeys.SaveUnconfirmed);
        await _storage.DidNotReceiveWithAnyArgs().DeleteAsync(default, default!, default);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Update_RemovalWithoutReplacementDoesNotPersistOrDeleteSavedImage()
    {
        _repository.GetAsync(Token).Returns(Existing());
        var result = await new UpdateFeaturedHandler(_repository, _storage, _user)
            .Handle(Update() with { Tile1 = Tile("First", remove: true) }, Token);
        result.Error.Should().Be(FeaturedErrorKeys.ImageRequired);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await _storage.DidNotReceiveWithAnyArgs().DeleteAsync(default, default!, default);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Read_AbsentSuccessfulNullAndHiddenRowSignsBothPrivatePreviews()
    {
        var handler = new GetFeaturedForAdminHandler(_repository, _storage);
        var absent = await handler.Handle(new(), Token);
        absent.IsSuccess.Should().BeTrue();
        absent.Value.Should().BeNull();
        await _storage.DidNotReceiveWithAnyArgs().CreateSignedUrlAsync(default, default!, default, default);
        _repository.GetAsync(Token).Returns(Existing());
        _storage.CreateSignedUrlAsync(StorageArea.FeaturedImages, Arg.Any<string>(), 900, Token)
            .Returns(call => "signed:" + call.ArgAt<string>(1));
        var result = await handler.Handle(new(), Token);
        result.Value!.Title.Should().Be("Saved");
        result.Value.IsVisible.Should().BeFalse();
        result.Value.Tile1.Image.PreviewUrl.Should().Be("signed:old-one");
        result.Value.Tile2.Image.PreviewUrl.Should().Be("signed:old-two");
        result.Value.Tile1.ButtonLink.Should().Be("/old");
        await _storage.Received(2).CreateSignedUrlAsync(StorageArea.FeaturedImages, Arg.Any<string>(), 900, Token);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task LoadFailuresAndPermissionLossReturnLocalizedKeysWithoutWrites()
    {
        foreach (var exception in new Exception[] { new DomainException(RbacErrorKeys.AccessDenied), new IOException("offline") })
        {
            _repository.GetAsync(Token).ThrowsAsync(exception);
            var expected = exception is DomainException ? RbacErrorKeys.AccessDenied : FeaturedErrorKeys.LoadFailed;
            (await new GetFeaturedForAdminHandler(_repository, _storage).Handle(new(), Token)).Error.Should().Be(expected);
            (await new CreateFeaturedHandler(_repository, _storage, _user).Handle(Create(), Token)).Error.Should().Be(expected);
            (await new UpdateFeaturedHandler(_repository, _storage, _user).Handle(Update(), Token)).Error.Should().Be(expected);
        }
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task CancellationPropagatesAndMissingIdentityCannotUpload()
    {
        _user.Id.Returns((Guid?)null);
        (await new CreateFeaturedHandler(_repository, _storage, _user).Handle(Create(), Token)).Error.Should().Be(RbacErrorKeys.AccessDenied);
        (await new UpdateFeaturedHandler(_repository, _storage, _user).Handle(Update(), Token)).Error.Should().Be(RbacErrorKeys.AccessDenied);
        await _storage.DidNotReceiveWithAnyArgs().UploadAsync(default, default, default!, default!, default!, default);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _repository.GetAsync(cts.Token).ThrowsAsync(new OperationCanceledException(cts.Token));
        Func<Task> read = async () => await new GetFeaturedForAdminHandler(_repository, _storage).Handle(new(), cts.Token);
        await read.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Authorization_CreateAndEditRequireViewAndMatchingActionBeforeValidation()
    {
        foreach (var create in new[] { true, false })
            foreach (var view in new[] { true, false })
                foreach (var action in new[] { true, false })
                {
                    var user = Substitute.For<ICurrentUserService>();
                    user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(view);
                    user.HasPermission(create ? PermissionCatalogue.Homepage.CreateCode : PermissionCatalogue.Homepage.EditCode).Returns(action);
                    var inspector = Substitute.For<IHeroBannerImageInspector>();
                    inspector.InspectAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
                        .Returns(new HeroBannerImageInspection("image/png", 1200, 1200, true));
                    var terminalCalled = false;
                    Result result;
                    if (create)
                    {
                        var request = Create() with { Title = "" };
                        var validation = new ValidationBehavior<CreateFeaturedCommand, Result>([new CreateFeaturedCommandValidator(inspector)]);
                        result = await new AuthorizationBehavior<CreateFeaturedCommand, Result>(user).Handle(request,
                            ct => validation.Handle(request, _ => { terminalCalled = true; return Task.FromResult(Result.Ok()); }, ct), Token);
                    }
                    else
                    {
                        var request = Update() with { Title = "" };
                        var validation = new ValidationBehavior<UpdateFeaturedCommand, Result>([new UpdateFeaturedCommandValidator(inspector)]);
                        result = await new AuthorizationBehavior<UpdateFeaturedCommand, Result>(user).Handle(request,
                            ct => validation.Handle(request, _ => { terminalCalled = true; return Task.FromResult(Result.Ok()); }, ct), Token);
                    }
                    result.Error.Should().Be(view && action ? FeaturedErrorKeys.SectionTitleRequired : RbacErrorKeys.AccessDenied);
                    terminalCalled.Should().BeFalse();
                }
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Authorization_ReadAndImageSelectionRequireViewEvenForCreateOrEditStaff()
    {
        foreach (var view in new[] { false, true })
        {
            _user.HasPermission(PermissionCatalogue.Homepage.ViewCode).Returns(view);
            _user.HasPermission(PermissionCatalogue.Homepage.CreateCode).Returns(true);
            _user.HasPermission(PermissionCatalogue.Homepage.EditCode).Returns(true);
            var read = await new AuthorizationBehavior<GetFeaturedForAdminQuery, Result<FeaturedDto?>>(_user)
                .Handle(new(), _ => Task.FromResult(Result.Ok<FeaturedDto?>(null)), Token);
            var selection = await new AuthorizationBehavior<ValidateFeaturedImageQuery, Result>(_user)
                .Handle(new(Image()), ct => new ValidateFeaturedImageHandler().Handle(new(Image()), ct), Token);
            read.IsSuccess.Should().Be(view);
            selection.IsSuccess.Should().Be(view);
            if (!view) { read.Error.Should().Be(RbacErrorKeys.AccessDenied); selection.Error.Should().Be(RbacErrorKeys.AccessDenied); }
        }
    }
}
