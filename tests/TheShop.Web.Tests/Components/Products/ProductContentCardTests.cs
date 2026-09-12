using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Web.Components.Common;
using TheShop.Web.Components.Products;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Products;

/// <summary>
/// Tests for <see cref="ProductContentCard"/>: description binding to <c>ShopRichTextEditor</c>
/// (FR-1, FR-2), the specification editor's add/edit/remove flow with order-by-index positions
/// (FR-4, RULE-3, RULE-4, Decision 10), server-flagged and client-detected duplicate rows
/// (AC-7, AC-8), and removal focus/announcement (AC-13, Decision 11).
/// <see href=".specs/product-description/spec.md"/>
/// </summary>
public class ProductContentCardTests : TestContext
{
    public ProductContentCardTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

    private static IRenderedComponent<ProductContentCard> RenderCard(
        TestContext context,
        string? description = null,
        IReadOnlyList<ProductSpecificationDto>? initialSpecifications = null,
        Action<string?>? descriptionChanged = null,
        Action<ProductSpecificationsState>? stateChanged = null,
        IReadOnlyList<int>? flaggedRowPositions = null) =>
        context.Render<ProductContentCard>(parameters => parameters
            .Add(c => c.Description, description)
            .Add(c => c.DescriptionChanged, descriptionChanged ?? (_ => { }))
            .Add(c => c.InitialSpecifications, initialSpecifications ?? [])
            .Add(c => c.StateChanged, stateChanged ?? (_ => { }))
            .Add(c => c.FlaggedRowPositions, flaggedRowPositions ?? []));

    // =========================================================================
    // Description (FR-1, FR-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public void Render_ShowsTheDescriptionLabelAndHeading()
    {
        var cut = RenderCard(this);

        cut.Markup.Should().Contain(Strings.ProductContent_Heading);
        cut.Markup.Should().Contain(Strings.AddProduct_DescriptionLabel);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task EditingTheDescription_RaisesDescriptionChanged()
    {
        string? raised = null;
        var cut = RenderCard(this, descriptionChanged: html => raised = html);

        await cut.InvokeAsync(() => cut.FindComponent<ShopRichTextEditor>().Instance.OnTextChanged("<p>New</p>", 3));

        raised.Should().Be("<p>New</p>");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task TextLengthChanged_UpdatesTheCounter()
    {
        var cut = RenderCard(this);

        await cut.InvokeAsync(() => cut.FindComponent<ShopRichTextEditor>().Instance.OnTextChanged("<p>Hello</p>", 5));

        cut.Markup.Should().Contain(string.Format(Strings.AddProduct_DescriptionCounter, 5, 20_000));
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task UnsupportedPaste_ShowsTheFormattingRemovedNotice()
    {
        var cut = RenderCard(this);

        await cut.InvokeAsync(() => cut.FindComponent<ShopRichTextEditor>().Instance.OnPasteUnsupported());

        cut.Markup.Should().Contain(Strings.AddProduct_DescriptionFormattingRemoved);
    }

    // =========================================================================
    // Specifications — seeding and add (FR-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public void Render_WithInitialSpecifications_SeedsTheStatePayloadInSavedOrder()
    {
        ProductSpecificationsState? raised = null;
        var initial = new[]
        {
            new ProductSpecificationDto(Guid.NewGuid(), "Material", "Stainless steel", 0),
            new ProductSpecificationDto(Guid.NewGuid(), "Capacity", "750 ml", 1),
        };

        RenderCard(this, initialSpecifications: initial, stateChanged: state => raised = state);

        raised.Should().NotBeNull();
        raised!.Specifications.Should().SatisfyRespectively(
            first => first.Name.Should().Be("Material"),
            second => second.Name.Should().Be("Capacity"));
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task AddRow_AppendsAnEmptyRowAtTheEnd()
    {
        ProductSpecificationsState? raised = null;
        var cut = RenderCard(this, stateChanged: state => raised = state);

        await cut.InvokeAsync(() => cut.FindComponent<MudButton>().Instance.OnClick.InvokeAsync());

        raised.Should().NotBeNull();
        raised!.Specifications.Should().ContainSingle(s => s.Name == string.Empty && s.Value == string.Empty);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task EditingARowsNameAndValue_RaisesStateChangedWithTheUpdatedRow()
    {
        ProductSpecificationsState? raised = null;
        var initial = new[] { new ProductSpecificationDto(Guid.NewGuid(), "Material", "Steel", 0) };
        var cut = RenderCard(this, initialSpecifications: initial, stateChanged: state => raised = state);

        var nameField = cut.FindComponents<MudTextField<string>>()[0];
        await cut.InvokeAsync(() => nameField.Instance.ValueChanged.InvokeAsync("Weight"));

        raised!.Specifications.Should().ContainSingle(s => s.Name == "Weight" && s.Value == "Steel");
    }

    // =========================================================================
    // Specifications — remove, focus, and announcement (AC-13, Decision 11)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task RemoveRow_WhenAnotherRowRemains_RaisesStateChangedWithoutTheRemovedRow()
    {
        ProductSpecificationsState? raised = null;
        var initial = new[]
        {
            new ProductSpecificationDto(Guid.NewGuid(), "Material", "Steel", 0),
            new ProductSpecificationDto(Guid.NewGuid(), "Capacity", "750 ml", 1),
        };
        var cut = RenderCard(this, initialSpecifications: initial, stateChanged: state => raised = state);

        await cut.Find($"[aria-label='{Strings.ProductSpecifications_RemoveRow}']").ClickAsync(new MouseEventArgs());

        raised!.Specifications.Should().ContainSingle().Which.Name.Should().Be("Capacity");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task RemoveRow_AnnouncesTheRemoval()
    {
        var initial = new[] { new ProductSpecificationDto(Guid.NewGuid(), "Material", "Steel", 0) };
        var cut = RenderCard(this, initialSpecifications: initial);

        await cut.Find($"[aria-label='{Strings.ProductSpecifications_RemoveRow}']").ClickAsync(new MouseEventArgs());

        cut.Markup.Should().Contain(Strings.ProductSpecifications_RowRemovedAnnouncement);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Render_WithNoSpecifications_ShowsTheEmptyState()
    {
        var cut = RenderCard(this);

        cut.Markup.Should().Contain(Strings.ProductSpecifications_EmptyStateTitle);
    }

    // =========================================================================
    // Duplicate/flagged rows (AC-7, AC-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public void Render_WithServerFlaggedRowPosition_MarksThatRowsFieldsInError()
    {
        var initial = new[] { new ProductSpecificationDto(Guid.NewGuid(), "Material", "", 0) };
        var cut = RenderCard(this, initialSpecifications: initial, flaggedRowPositions: [0]);

        cut.FindComponents<MudTextField<string>>().Should().Contain(f => f.Instance.Error);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public async Task Render_WithTwoRowsSharingANameIgnoringCaseAndSpaces_FlagsBothClientSide()
    {
        var initial = new[]
        {
            new ProductSpecificationDto(Guid.NewGuid(), "Material", "Steel", 0),
            new ProductSpecificationDto(Guid.NewGuid(), " material ", "Aluminum", 1),
        };
        var cut = RenderCard(this, initialSpecifications: initial);
        await cut.InvokeAsync(() => { });

        var nameFields = cut.FindComponents<MudTextField<string>>()
            .Where((_, index) => index % 2 == 0)
            .ToList();
        nameFields.Should().OnlyContain(f => f.Instance.Error,
            "duplicate names are flagged client-side before any round trip (RULE-4)");
    }
}
