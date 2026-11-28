namespace TheShop.Application.Features.Homepage;

/// <summary>Resource keys returned by homepage brand use cases.</summary>
public static class HomepageBrandErrorKeys
{
    /// <summary>Title is blank.</summary>
    public const string TitleRequired = "HomepageBrand_TitleRequired";
    /// <summary>Button label is blank.</summary>
    public const string ButtonLabelRequired = "HomepageBrand_ButtonLabelRequired";
    /// <summary>Button link is blank.</summary>
    public const string ButtonLinkRequired = "HomepageBrand_ButtonLinkRequired";
    /// <summary>Button link is unsafe or malformed.</summary>
    public const string ButtonLinkInvalid = "HomepageBrand_ButtonLinkInvalid";
    /// <summary>Brand count or uniqueness is invalid.</summary>
    public const string SelectionInvalid = "HomepageBrand_SelectionInvalid";
    /// <summary>Selected brand is missing or inactive.</summary>
    public const string BrandUnavailable = "HomepageBrand_BrandUnavailable";
    /// <summary>The singleton already exists.</summary>
    public const string AlreadyExists = "HomepageBrand_AlreadyExists";
    /// <summary>The singleton is absent during update.</summary>
    public const string NotFound = "HomepageBrand_NotFound";
    /// <summary>Section load failed.</summary>
    public const string LoadFailed = "HomepageBrand_LoadFailed";
    /// <summary>Section save failed.</summary>
    public const string SaveFailed = "HomepageBrand_SaveFailed";
    /// <summary>Brand choices load failed.</summary>
    public const string ChoicesLoadFailed = "HomepageBrand_ChoicesLoadFailed";
}
