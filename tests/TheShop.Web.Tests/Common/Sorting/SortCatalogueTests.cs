using System.Globalization;
using FluentAssertions;
using TheShop.Web.Common.Sorting;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Common.Sorting;

// The contract every feature's sort catalogue must satisfy, written once and inherited per feature.
// The completeness test is the point of the base class: adding an enum member and forgetting to
// register it used to be silent — FromSlug would quietly hand back the default and a shared link
// would sort by the wrong thing — so it is asserted here rather than left to each feature to
// remember.
public abstract class SortCatalogueTests<TSort> where TSort : struct, Enum
{
    protected abstract SortCatalogue<TSort> Catalogue { get; }

    [Fact]
    public void Options_CoverEveryMemberOfTheEnum()
    {
        var registered = Catalogue.Options.Select(option => option.Value);

        Enum.GetValues<TSort>().Should().BeEquivalentTo(
            registered,
            "an unregistered sort option is unreachable from the picker and falls back to the default in a link");
    }

    [Fact]
    public void ToSlug_ThenFromSlug_RoundTripsEveryOption()
    {
        foreach (var option in Catalogue.Options)
            Catalogue.FromSlug(Catalogue.ToSlug(option.Value)).Should().Be(option.Value);
    }

    [Fact]
    public void Slugs_AreDistinctAcrossOptions()
    {
        Catalogue.Options.Select(option => option.Slug).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void LabelKeys_AreAllPopulated()
    {
        Catalogue.Options.Should().OnlyContain(option => !string.IsNullOrWhiteSpace(option.LabelKey));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    public void LabelKeys_ResolveToRealResourceStrings_InEveryShippedLanguage(string culture)
    {
        // The keys are plain strings by the time they reach the localizer, so a typo or a resx entry
        // that only exists in English would surface as the raw key rendered in the picker. Resolving
        // each one against both cultures turns that into a build-time failure instead.
        var resources = new System.Resources.ResourceManager(typeof(Strings));
        var cultureInfo = CultureInfo.GetCultureInfo(culture);

        foreach (var option in Catalogue.Options)
        {
            resources.GetString(option.LabelKey, cultureInfo)
                .Should().NotBeNullOrWhiteSpace($"'{option.LabelKey}' must exist in Strings.{culture}.resx");
        }
    }

    [Fact]
    public void Default_IsOneOfTheOfferedOptions()
    {
        Catalogue.Options.Select(option => option.Value).Should().Contain(Catalogue.Default);
    }

    [Fact]
    public void Picker_MirrorsTheDeclaredOptionsInOrder()
    {
        Catalogue.Picker.Should().Equal(Catalogue.Options.Select(option => (option.Value, option.LabelKey)));
    }

    [Fact]
    public void FromSlug_UnknownSlug_FallsBackToTheDefault()
    {
        Catalogue.FromSlug("not-a-real-slug").Should().Be(Catalogue.Default);
    }

    [Fact]
    public void FromSlug_Null_FallsBackToTheDefault()
    {
        Catalogue.FromSlug(null).Should().Be(Catalogue.Default);
    }
}
