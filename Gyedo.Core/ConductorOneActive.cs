using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Gyedo;

public partial class Conductor<T>
{
    /// <summary>컬렉션 기반 Conductor입니다.</summary>
    public partial class Collection
    {
        /// <summary>여러 화면을 보관하고 하나만 활성화합니다.</summary>
        public class OneActive : ConductorBaseWithActiveItem<T>
        {
            private readonly BindableCollection<T> items = new();
            /// <summary>화면 목록입니다. 변경은 AddItemAsync/CloseItemAsync/ClearAsync를 사용합니다.</summary>
            public ReadOnlyObservableCollection<T> Items { get; }
            /// <summary>빈 화면 목록을 생성합니다.</summary>
            public OneActive() => this.Items = new(this.items);
            /// <summary>활성화하지 않고 화면을 목록에 추가합니다.</summary>
            public Task AddItemAsync(T item) => this.RunNavigationAsync(async () =>
            {
                if (item == null || this.items.Contains(item)) return;
                this.EnsureItem(item);
                await ScreenExtensions.TryDeactivateAsync(item);
            });
            /// <inheritdoc/>
            public override IEnumerable<T> GetChildren() => this.items.ToArray();
            /// <inheritdoc/>
            public override Task<bool> ActivateItemAsync(T item) => this.RunNavigationAsync(async () =>
            {
                await this.ChangeActiveItemAsync(item, false);
                return true;
            });
            /// <inheritdoc/>
            public override Task DeactivateItemAsync(T item) => this.RunNavigationAsync(async () =>
            {
                if (item != null && ReferenceEquals(item, this.ActiveItem))
                    await this.ChangeActiveItemAsync(this.DetermineNextItemToActivate(item), false);
                else
                    await ScreenExtensions.TryDeactivateAsync(item);
            });
            /// <inheritdoc/>
            public override Task<bool> CloseItemAsync(T item) => this.CloseOwnedItemAsync(item, async () =>
            {
                bool active = ReferenceEquals(item, this.ActiveItem);
                T next = active ? this.DetermineNextItemToActivate(item) : null;
                await this.CloseAndCleanUpAsync(item, this.DisposeChildren);
                this.items.Remove(item);
                if (active)
                {
                    this.SetActiveItem(null);
                    await this.ChangeActiveItemAsync(next, false);
                }
                return true;
            });
            /// <summary>모든 화면의 종료 허용 여부를 확인한 뒤 종료·폐기합니다.</summary>
            public Task<bool> ClearAsync() => this.ClearOwnedItemsAsync(this.CloseChildrenAsync);
            /// <summary>종료한 화면 대신 선택할 화면을 결정합니다.</summary>
            protected virtual T DetermineNextItemToActivate(T item)
            {
                int index = this.items.IndexOf(item);
                return index > 0 ? this.items[index - 1] : this.items.FirstOrDefault(x => !ReferenceEquals(x, item));
            }
            /// <inheritdoc/>
            public override Task<bool> CanCloseAsync() => this.CanAllItemsCloseAsync(this.items);
            /// <inheritdoc/>
            protected override Task OnClosedAsync() => this.RunNavigationAsync(this.CloseChildrenAsync);
            private async Task CloseChildrenAsync()
            {
                foreach (T item in this.items.ToArray())
                {
                    await this.CloseAndCleanUpAsync(item, this.DisposeChildren);
                    this.items.Remove(item);
                    if (ReferenceEquals(item, this.ActiveItem)) this.SetActiveItem(null);
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
