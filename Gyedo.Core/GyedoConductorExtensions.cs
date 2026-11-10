using System.Threading.Tasks;

namespace Gyedo;

/// <summary>Conductor의 자식 종료와 소유권 정리를 수행합니다.</summary>
public static class GyedoConductorExtensions
{
    /// <summary>종료 완료 후 부모 연결을 해제하고 필요한 경우 자식의 폐기를 기다립니다.</summary>
    public static async Task CloseAndCleanUpAsync<T>(this IConductor<T> parent, T item, bool dispose) where T : class
    {
        await ScreenExtensions.TryCloseAsync(item);
        if (item is IChild child && ReferenceEquals(child.Parent, parent))
            child.Parent = null;
        if (dispose)
            await ScreenExtensions.TryDisposeAsync(item);
        if (parent is ConductorBase<T> owner)
            owner.ForgetItem(item);
    }
}
