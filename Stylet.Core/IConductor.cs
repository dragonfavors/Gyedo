using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stylet;

/// <summary>자식 화면을 소유합니다.</summary>
public interface IParent<out T>
{
    /// <summary>소유한 자식의 스냅샷을 반환합니다.</summary>
    IEnumerable<T> GetChildren();
}
/// <summary>현재 선택한 화면을 제공합니다.</summary>
public interface IHaveActiveItem<out T>
{
    /// <summary>선택 변경은 ActivateItemAsync를 사용합니다.</summary>
    T ActiveItem { get; }
}
/// <summary>WPF 등의 선택 UI가 비동기 화면 전환을 호출하는 계약입니다.</summary>
public interface IActiveItemConductor
{
    /// <summary>선택된 화면입니다.</summary>
    object ActiveItem { get; }
    /// <summary>화면 전환이 허용되면 완료까지 기다리고 true를 반환합니다.</summary>
    Task<bool> ActivateItemAsync(object item);
}
/// <summary>자식의 종료 요청을 처리합니다.</summary>
public interface IChildDelegate
{
    /// <summary>종료를 거절하면 false, 완료하면 true를 반환합니다.</summary>
    Task<bool> CloseItemAsync(object item, bool? dialogResult = null);
}
/// <summary>자식의 비동기 수명주기를 관리합니다.</summary>
public interface IConductor<T>
{
    /// <summary>종료 완료 후 자식을 폐기할지 지정합니다.</summary>
    bool DisposeChildren { get; set; }
    /// <summary>화면 전환을 기다립니다. 이전 화면의 종료가 거절되면 false입니다.</summary>
    Task<bool> ActivateItemAsync(T item);
    /// <summary>비활성화 완료를 기다립니다.</summary>
    Task DeactivateItemAsync(T item);
    /// <summary>종료 허용 여부를 확인하고 종료·정리 완료를 기다립니다.</summary>
    Task<bool> CloseItemAsync(T item);
}
