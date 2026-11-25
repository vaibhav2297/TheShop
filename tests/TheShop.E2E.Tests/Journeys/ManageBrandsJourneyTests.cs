using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages.Admin;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>
/// Manage Brands + Add Brand admin journeys (.specs/manage-brands/spec.md and
/// .specs/add-brand/spec.md §6). Shared-database discipline: every created brand uses a unique
/// generated name so reruns never collide; there is no cleanup step since these are harmless,
/// clearly-marked "e2e-brand-" rows on a stack that gets a full `supabase db reset` between
/// development sessions.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "manage-brands")]
public sealed class ManageBrandsJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)
{
    [Fact]
    [Trait("Feature", "add-brand")]
    public async Task AddBrand_AC1_Admin_creates_a_brand_and_sees_it_listed()
    {
        var brandName = $"e2e-brand-{Guid.NewGuid():N}";

        var manageBrands = new ManageBrandsPage(Page);
        await manageBrands.GotoAsync();
        await manageBrands.GotoAddBrandAsync();

        var addBrand = new AddBrandPage(Page);
        await addBrand.CreateAsync(brandName);

        // Handler navigates back to Routes.Admin.ManageBrands on success.
        await Page.WaitForURLAsync(url => url.Contains("/admin/brands") && !url.Contains("/new"),
            new() { Timeout = 15_000 });
        await manageBrands.BrandRow(brandName).WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC6_Admin_edits_a_brand_and_it_persists()
    {
        var originalName = $"e2e-brand-{Guid.NewGuid():N}";
        var renamedTo = $"e2e-brand-{Guid.NewGuid():N}";

        var manageBrands = new ManageBrandsPage(Page);
        await manageBrands.GotoAsync();
        await manageBrands.GotoAddBrandAsync();

        var addBrand = new AddBrandPage(Page);
        await addBrand.CreateAsync(originalName);
        await manageBrands.BrandRow(originalName).WaitForAsync(new() { Timeout = 15_000 });

        await manageBrands.GotoEditBrandAsync(originalName);
        var editBrand = new EditBrandPage(Page);
        await editBrand.RenameAsync(renamedTo);

        await Page.WaitForURLAsync(url => url.Contains("/admin/brands") && !url.Contains("/edit"),
            new() { Timeout = 15_000 });
        await manageBrands.BrandRow(renamedTo).WaitForAsync(new() { Timeout = 15_000 });
    }
}
