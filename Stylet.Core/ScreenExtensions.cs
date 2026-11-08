using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stylet;

/// <summary>선택적인 화면 수명주기와 자원 정리를 기다립니다.</summary>
public static class ScreenExtensions
{
    /// <summary>화면이면 활성화를 기다립니다.</summary>
    public static Task TryActivateAsync(object screen, CancellationToken cancellationToken = default) =>
        screen is IScreenState state ? state.ActivateAsync(cancellationToken) : Task.CompletedTask;
    /// <summary>화면이면 비활성화를 기다립니다.</summary>
    public static Task TryDeactivateAsync(object screen) =>
        screen is IScreenState state ? state.DeactivateAsync() : Task.CompletedTask;
    /// <summary>종료 허용 여부를 확인하지 않고 화면 종료를 기다립니다.</summary>
    public static Task TryCloseAsync(object screen) =>
        screen is IScreenState state ? state.CloseAsync() : Task.CompletedTask;
    /// <summary>IAsyncDisposable을 우선 사용하며, 없으면 IDisposable을 호출합니다.</summary>
    public static async Task TryDisposeAsync(object screen)
    {
        if (screen is IAsyncDisposable asynchronous)
            await asynchronous.DisposeAsync();
        else if (screen is IDisposable synchronous)
            synchronous.Dispose();
    }
}
