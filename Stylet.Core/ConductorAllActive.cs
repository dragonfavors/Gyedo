using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stylet;

public partial class Conductor<T>
{
    public partial class Collection
    {
        /// <summary>소유한 모든 화면을 함께 활성화합니다.</summary>
        public class AllActive : ConductorBase<T>
        {
            private readonly BindableCollection<T> items = new();
            /// <summary>변경은 ActivateItemAsync/CloseItemAsync/ClearAsync를 사용합니다.</summary>
            public ReadOnlyObservableCollection<T> Items { get; }
            /// <summary>빈 화면 목록을 생성합니다.</summary>
            public AllActive() => this.Items = new(this.items);
            /// <inheritdoc/>
            public override IEnumerable<T> GetChildren() => this.items.ToArray();
            /// <inheritdoc/>
            public override Task<bool> ActivateItemAsync(T item) => this.RunNavigationAsync(async () =>
            {
                if (item == null) return false;
                this.EnsureItem(item);
                if (this.IsActive) await this.ActivateChildAsync(item, this.ActivationCancellationToken);
                else await ScreenExtensions.TryDeactivateAsync(item);
                return true;
            });
            /// <inheritdoc/>
            public override Task DeactivateItemAsync(T item) => this.RunNavigationAsync(() => ScreenExtensions.TryDeactivateAsync(item));
            /// <inheritdoc/>
            public override Task<bool> CloseItemAsync(T item) => this.CloseOwnedItemAsync(item, async () =>
            {
                await this.CloseAndCleanUpAsync(item, this.DisposeChildren);
                this.items.Remove(item);
                return true;
            });
            /// <summary>전체 종료 허용 여부를 확인하고 종료·폐기를 기다립니다.</summary>
            public Task<bool> ClearAsync() => this.ClearOwnedItemsAsync(this.CloseChildrenAsync);
            /// <inheritdoc/>
            public override Task<bool> CanCloseAsync() => this.CanAllItemsCloseAsync(this.items);
            /// <inheritdoc/>
            protected override Task OnActivatedAsync(CancellationToken cancellationToken) => this.RunNavigationAsync(async () =>
            {
                foreach (T item in this.items.ToArray()) await this.ActivateChildAsync(item, cancellationToken);
            });
            /// <inheritdoc/>
            protected override Task OnDeactivatedAsync() => this.RunNavigationAsync(async () =>
            {
                foreach (T item in this.items.ToArray()) await ScreenExtensions.TryDeactivateAsync(item);
            });
            /// <inheritdoc/>
            protected override Task OnClosedAsync() => this.RunNavigationAsync(this.CloseChildrenAsync);
            private async Task CloseChildrenAsync()
            {
                foreach (T item in this.items.ToArray())
                {
                    await this.CloseAndCleanUpAsync(item, this.DisposeChildren);
                    this.items.Remove(item);
                }
            }
            /// <inheritdoc/>
            protected override void EnsureItem(T item)
            {
                base.EnsureItem(item);
                if (!this.items.Contains(item)) this.items.Add(item);
            }
        }
    }
}
