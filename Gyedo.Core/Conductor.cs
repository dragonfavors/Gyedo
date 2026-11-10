using System.Threading.Tasks;

namespace Gyedo;

/// <summary>화면을 교체할 때 이전 화면의 종료·폐기를 기다리는 Conductor입니다.</summary>
public partial class Conductor<T> : ConductorBaseWithActiveItem<T> where T : class
{
    /// <inheritdoc/>
    public override Task<bool> ActivateItemAsync(T item) => this.RunNavigationAsync(async () =>
    {
        if (!ReferenceEquals(item, this.ActiveItem) && !await this.CanCloseItemAsync(this.ActiveItem))
            return false;
        await this.ChangeActiveItemAsync(item, true);
        return true;
    });
    /// <inheritdoc/>
    public override Task DeactivateItemAsync(T item) => this.RunNavigationAsync(async () =>
    {
        if (ReferenceEquals(item, this.ActiveItem))
            await ScreenExtensions.TryDeactivateAsync(item);
    });
    /// <inheritdoc/>
    public override Task<bool> CloseItemAsync(T item) => this.CloseOwnedItemAsync(item, async () =>
    {
        if (ReferenceEquals(item, this.ActiveItem))
            await this.ChangeActiveItemAsync(null, true);
        else
            await this.CloseAndCleanUpAsync(item, this.DisposeChildren);
        return true;
    });
    /// <inheritdoc/>
    public override Task<bool> CanCloseAsync() => this.CanAllItemsCloseAsync(this.OwnedItems);
}
