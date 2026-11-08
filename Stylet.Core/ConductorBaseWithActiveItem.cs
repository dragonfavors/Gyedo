using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stylet;

/// <summary>한 번에 하나의 자식을 선택하는 Conductor 기반입니다.</summary>
public abstract class ConductorBaseWithActiveItem<T> : ConductorBase<T>, IHaveActiveItem<T>, IActiveItemConductor where T : class
{
    private T activeItem;
    /// <inheritdoc/>
    public T ActiveItem => this.activeItem;
    object IActiveItemConductor.ActiveItem => this.ActiveItem;
    Task<bool> IActiveItemConductor.ActivateItemAsync(object item) =>
        item == null || item is T ? this.ActivateItemAsync((T)item) : Task.FromResult(false);
    /// <inheritdoc/>
    public override IEnumerable<T> GetChildren() => this.OwnedItems;
    /// <summary>수명주기를 처리한 뒤 선택 속성을 갱신합니다.</summary>
    protected void SetActiveItem(T item)
    {
        this.activeItem = item;
        this.OnPropertyChanged(nameof(this.ActiveItem));
    }
    /// <summary>이전 화면을 정리하고 새 화면의 활성화가 끝난 뒤 선택을 확정합니다.</summary>
    protected virtual async Task ChangeActiveItemAsync(T item, bool closePrevious)
    {
        T previous = this.ActiveItem;
        this.ValidateItemOwnership(item);
        if (ReferenceEquals(previous, item))
        {
            if (this.IsActive)
                await this.ActivateChildAsync(item, this.ActivationCancellationToken);
            return;
        }
        await ScreenExtensions.TryDeactivateAsync(previous);
        if (closePrevious)
        {
            await this.CloseAndCleanUpAsync(previous, this.DisposeChildren);
            this.SetActiveItem(null);
        }
        if (item != null)
        {
            this.EnsureItem(item);
            try
            {
                if (this.IsActive)
                    await this.ActivateChildAsync(item, this.ActivationCancellationToken);
                else
                    await ScreenExtensions.TryDeactivateAsync(item);
            }
            catch
            {
                if (!closePrevious && this.IsActive && !this.ActivationCancellationToken.IsCancellationRequested)
                    await this.ActivateChildAsync(previous, this.ActivationCancellationToken);
                throw;
            }
        }
        this.SetActiveItem(item);
    }
    /// <inheritdoc/>
    protected override Task OnActivatedAsync(CancellationToken cancellationToken) =>
        this.RunNavigationAsync(() => this.ActivateChildAsync(this.ActiveItem, cancellationToken));
    /// <inheritdoc/>
    protected override Task OnDeactivatedAsync() =>
        this.RunNavigationAsync(() => ScreenExtensions.TryDeactivateAsync(this.ActiveItem));
    /// <inheritdoc/>
    protected override Task OnClosedAsync() => this.RunNavigationAsync(async () =>
    {
        foreach (T item in this.OwnedItems)
            await this.CloseAndCleanUpAsync(item, this.DisposeChildren);
        this.SetActiveItem(null);
    });
}
