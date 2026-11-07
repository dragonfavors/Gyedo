using System;

namespace Stylet;

/// <summary>
/// UI 타입에 의존하지 않고 View 로딩과 연결 해제를 알립니다.
/// </summary>
public interface IViewLifecycleAware
{
    /// <summary>
    /// 종료 콜백 전에 기존 View 연결을 해제하도록 동기 알림을 보냅니다.
    /// UI 어댑터는 캐시를 즉시 비우고 UI 이벤트 정리는 해당 스레드에서 처리합니다.
    /// </summary>
    event EventHandler ViewReleaseRequested;

    /// <summary>
    /// UI 어댑터가 View 로딩을 알립니다. 화면 활성화는 별도로 처리합니다.
    /// </summary>
    void ViewLoaded();
}
