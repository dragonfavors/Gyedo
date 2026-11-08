using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stylet;

/// <summary>비동기 탐색과 자식 소유권을 관리하는 공통 기반입니다.</summary>
public abstract class ConductorBase<T> : Screen, IConductor<T>, IParent<T>, IChildDelegate where T : class
{
    private readonly AsyncOperationQueue navigation = new();
    private readonly AsyncOperationQueue closeGuards = new();
    private readonly HashSet<T> ownedItems = new(ReferenceEqualityComparer.Instance);
    private T activatingItem;
    private CancellationTokenSource pendingActivation;

    /// <summary>활성화 도중 실패한 자식도 포함한 소유 목록입니다.</summary>
    protected IEnumerable<T> OwnedItems => this.ownedItems.ToArray();
    /// <inheritdoc/>
    public virtual bool DisposeChildren { get; set; } = true;
    /// <inheritdoc/>
    public abstract IEnumerable<T> GetChildren();
    /// <inheritdoc/>
    public abstract Task<bool> ActivateItemAsync(T item);
    /// <inheritdoc/>
    public abstract Task DeactivateItemAsync(T item);
    /// <inheritdoc/>
    public abstract Task<bool> CloseItemAsync(T item);
    /// <summary>탐색 작업을 호출 순서대로 실행합니다.</summary>
    protected Task RunNavigationAsync(Func<Task> operation) => this.navigation.RunAsync(operation);
    /// <summary>결과가 있는 탐색 작업을 호출 순서대로 실행합니다.</summary>
    protected Task<TResult> RunNavigationAsync<TResult>(Func<Task<TResult>> operation) => this.navigation.RunAsync(operation);
    /// <summary>자식을 현재 Conductor에 연결합니다.</summary>
    protected virtual void EnsureItem(T item)
    {
        if (item == null) return;
        this.ValidateItemOwnership(item);
        if (item is IChild child)
        {
            child.Parent = this;
        }
        this.ownedItems.Add(item);
    }

    /// <summary>다른 부모가 소유한 화면으로 현재 화면을 교체하지 않도록 먼저 검사합니다.</summary>
    protected void ValidateItemOwnership(T item)
    {
        if (item is IChild child && child.Parent != null && !ReferenceEquals(child.Parent, this))
            throw new InvalidOperationException("다른 부모가 소유한 화면은 먼저 종료하거나 연결을 해제해야 합니다.");
    }

    internal void ForgetItem(T item) => this.ownedItems.Remove(item);

    /// <summary>종료 요청에서 탐색 큐를 기다리기 전에 취소할 수 있도록 자식 활성화를 추적합니다.</summary>
    protected async Task ActivateChildAsync(T item, CancellationToken cancellationToken)
    {
        if (item == null) return;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        this.activatingItem = item;
        this.pendingActivation = cancellation;
        try { await ScreenExtensions.TryActivateAsync(item, cancellation.Token); }
        finally
        {
            this.activatingItem = null;
            this.pendingActivation = null;
        }
    }

    /// <summary>종료가 허용되면 대기 중인 자식 활성화를 먼저 취소하고 탐색 큐에서 종료합니다.</summary>
    protected async Task<bool> CloseOwnedItemAsync(T item, Func<Task<bool>> close)
    {
        if (item == null || !this.ownedItems.Contains(item)) return false;
        if (!await this.closeGuards.RunAsync(() => this.CanCloseItemAsync(item))) return false;
        if (ReferenceEquals(item, this.activatingItem))
            this.pendingActivation.Cancel();
        return await this.RunNavigationAsync(async () =>
        {
            if (!this.ownedItems.Contains(item)) return false;
            return await close();
        });
    }

    /// <summary>전체 종료가 허용되면 진행 중인 자식 로딩을 취소하고 컬렉션 정리를 기다립니다.</summary>
    protected async Task<bool> ClearOwnedItemsAsync(Func<Task> close)
    {
        if (!await this.closeGuards.RunAsync(() => this.CanAllItemsCloseAsync(this.OwnedItems))) return false;
        this.pendingActivation?.Cancel();
        await this.RunNavigationAsync(close);
        return true;
    }
    /// <summary>자식의 종료 조건을 순서대로 확인합니다.</summary>
    protected virtual async Task<bool> CanAllItemsCloseAsync(IEnumerable<T> items)
    {
        foreach (T item in items.ToArray())
            if (!await this.CanCloseItemAsync(item))
                return false;
        return true;
    }
    /// <summary>자식 하나의 종료 조건을 확인합니다.</summary>
    protected virtual Task<bool> CanCloseItemAsync(T item) =>
        item is IGuardClose guard ? guard.CanCloseAsync() : Task.FromResult(true);
    /// <inheritdoc/>
    Task<bool> IChildDelegate.CloseItemAsync(object item, bool? dialogResult) =>
        item is T child ? this.CloseItemAsync(child) : Task.FromResult(false);
}
