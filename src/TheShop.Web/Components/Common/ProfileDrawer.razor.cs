using TheShop.Web.Common.Notifications;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using TheShop.Application.Features.Auth.Commands.SignOut;
using TheShop.Application.Features.Customers.Queries.GetCurrentCustomerProfile;
using TheShop.Web.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Account drawer composed by MainLayout for authenticated users; preserves profile navigation and sign-out.
/// </summary>
public partial class ProfileDrawer : ComponentBase, IDisposable
{
    [Inject] private IMediator Mediator { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IShopNotificationService Notifications { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;
    [Inject] private BusyState BusyState { get; set; } = default!;

    /// <summary>Layout-owned visibility of the account drawer.</summary>
    [Parameter] public bool Open { get; set; }

    /// <summary>Notifies the layout when the drawer closes or a destination is selected.</summary>
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }

    private string? _displayName;
    private string? _email;
    private readonly CancellationTokenSource _lifetime = new();

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var result = await Mediator.Send(new GetCurrentCustomerProfileQuery(), _lifetime.Token);
            if (result.IsSuccess && !_lifetime.IsCancellationRequested)
            {
                var profile = result.Value;
                _displayName = $"{profile.FirstName} {profile.LastName}".Trim();
                _email = profile.Email;
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }

    private Task CloseAsync() => OpenChanged.InvokeAsync(false);

    private async Task OnSignOutAsync()
    {
        if (BusyState.IsBusy(BusyKeys.Global)) return;
        await BusyState.RunAsync(BusyKeys.Global, async () =>
        {
            await CloseAsync();
            await Mediator.Send(new SignOutCommand(), CancellationToken.None);
            Notifications.Show(Localizer[nameof(Strings.Auth_SignedOut)], ShopNotificationKind.Success);
            Nav.NavigateTo(Routes.Home);
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
