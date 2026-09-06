using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;
using TheShop.Application.Features.Brands.Commands.CreateBrand;
using TheShop.Application.Features.Brands.DTOs;
using TheShop.Web.Auth;
using TheShop.Web.Common;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.State;

namespace TheShop.Web.Pages.Admin;

/// <summary>
/// Creates a new brand (Figma node <c>2465:1128</c>). Dispatches
/// <see cref="CreateBrandCommand"/>; on success returns to
/// <see cref="Routes.Admin.ManageBrands"/> (Flow 1). Requires a signed-in user via
/// <c>Pages/Admin/_Imports.razor</c>, and is gated on
/// <c>brands.create</c> within the page itself.
/// </summary>
[Route(Routes.Admin.AddBrand)]
[AuthorizePermission("brands.create")]
public partial class AddBrand : ComponentBase
{

    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 250;

    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;
    [Inject] private BreadcrumbState Breadcrumbs { get; set; } = default!;

    private MudForm _form = default!;
    private string _name = string.Empty;
    private string? _description;
    private bool _isActive = true;
    private bool _isFormValid;

    private IReadOnlyList<ShopUploadedImage> _logoImages = [];

    private readonly Func<string, string?> _nameValidation = name =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length > MaxNameLength
            ? Strings.Brand_NameTooLong
            : null;

    private readonly Func<string, string?> _descriptionValidation = description =>
        !string.IsNullOrWhiteSpace(description) && description.Trim().Length > MaxDescriptionLength
            ? Strings.Brand_DescriptionTooLong
            : null;

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        Breadcrumbs.Set(BreadcrumbTrail.Admin()
            .Add(Strings.ManageBrands_Heading, Routes.Admin.ManageBrands)
            .Current(Strings.AddBrand_Heading));
    }

    private void OnLogoImagesChanged(IReadOnlyList<ShopUploadedImage> images) => _logoImages = images;

    private async Task SaveAsync()
    {
        await _form.ValidateAsync();
        if (!_isFormValid) return;

        await BusyState.RunAsync(BusyKeys.Brands.AddBrand, async () =>
        {
            // A row still showing a validation error (wrong type / too large) never leaves the client.
            var validLogoImages = _logoImages.Where(image => image.Error is null).ToList();
            var logo = validLogoImages.Count > 0
                ? new BrandLogoUpload(validLogoImages[0].Bytes, validLogoImages[0].FileName, validLogoImages[0].ContentType)
                : null;

            var result = await Mediator.Send(new CreateBrandCommand(
                _name.Trim(),
                string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
                logo,
                _isActive));

            if (result.IsSuccess)
            {
                Snackbar.Add(Strings.Brand_Created, Severity.Success);
                Nav.NavigateTo(Routes.Admin.ManageBrands);
            }
            else
            {
                Snackbar.Add(Localizer[result.Error ?? nameof(Strings.Brand_CreateFailed)], Severity.Error);
            }
        });
    }
}
