using System.ComponentModel;
using System.Windows;

namespace Gyedo;

/// <summary>
/// WPF 디자이너 실행 여부를 확인합니다.
/// </summary>
public static class DesignMode
{
    private static bool? isEnabled;

    /// <summary>
    /// 디자인 모드 여부입니다. 테스트에서 명시적으로 설정할 수 있습니다.
    /// </summary>
    public static bool IsEnabled
    {
        get => isEnabled ??= (bool)DesignerProperties.IsInDesignModeProperty
            .GetMetadata(typeof(FrameworkElement)).DefaultValue;
        set => isEnabled = value;
    }
}
