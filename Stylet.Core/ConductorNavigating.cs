using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stylet;

public partial class Conductor<T>
{
    /// <summary>이전 화면을 보관하며 뒤로 탐색할 수 있는 Conductor입니다.</summary>
    public class StackNavigation : ConductorBaseWithActiveItem<T>
    {
        private readonly List<T> history = new();
        /// <inheritdoc/>
        public override IEnumerable<T> GetChildren() => this.OwnedItems;
        /// <inheritdoc/>
        public override Task<bool> ActivateItemAsync(T item) => this.RunNavigationAsync(async () =>
        {
            T previous = this.ActiveItem;
            await this.ChangeActiveItemAsync(item, false);
            if (previous != null && !ReferenceEquals(previous, item))
            {
                this.history.Remove(item);
                this.history.Add(previous);
            }
            return true;
        });
        /// <inheritdoc/>
        public override Task DeactivateItemAsync(T item) => this.RunNavigationAsync(() => ScreenExtensions.TryDeactivateAsync(item));
        /// <summary>현재 화면을 닫고 이전 화면을 활성화합니다.</summary>
        public Task<bool> GoBackAsync() => this.CloseItemAsync(this.ActiveItem);
        /// <summary>이전 화면들의 종료 조건을 확인한 뒤 이력을 비웁니다.</summary>
        public Task<bool> ClearAsync() => this.RunNavigationAsync(async () =>
        {
            if (!await this.CanAllItemsCloseAsync(this.history)) return false;
            foreach (T item in this.history.ToArray())
            {
                await this.CloseAndCleanUpAsync(item, this.DisposeChildren);
                this.history.Remove(item);
            }
            return true;
        });
        /// <inheritdoc/>
        public override Task<bool> CloseItemAsync(T item) => this.CloseOwnedItemAsync(item, async () =>
        {
            if (ReferenceEquals(item, this.ActiveItem))
            {
                T next = this.history.LastOrDefault();
                await this.ChangeActiveItemAsync(next, true);
                if (next != null) this.history.Remove(next);
            }
            else
            {
                await this.CloseAndCleanUpAsync(item, this.DisposeChildren);
                this.history.Remove(item);
            }
            return true;
        });
        /// <inheritdoc/>
        public override Task<bool> CanCloseAsync() => this.CanAllItemsCloseAsync(this.GetChildren());
        /// <inheritdoc/>
        protected override Task OnClosedAsync() => this.RunNavigationAsync(async () =>
        {
            foreach (T item in this.GetChildren())
            {
                await this.CloseAndCleanUpAsync(item, this.DisposeChildren);
                this.history.Remove(item);
                if (ReferenceEquals(item, this.ActiveItem)) this.SetActiveItem(null);
            }
        });
    }
}
