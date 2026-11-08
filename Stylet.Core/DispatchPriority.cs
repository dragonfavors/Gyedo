namespace Stylet;

/// <summary>UI 프레임워크와 독립적인 상대적 게시 우선순위입니다.</summary>
public enum DispatchPriority
{
    /// <summary>기본 우선순위. WPF에서는 Background에 대응합니다.</summary>
    Lowest = 0,
    /// <summary>WPF에서는 Input에 대응합니다.</summary>
    Low = 1,
    /// <summary>WPF에서는 Loaded에 대응합니다.</summary>
    Middle = 2,
    /// <summary>WPF에서는 Render에 대응합니다.</summary>
    High = 3,
    /// <summary>WPF에서는 Normal에 대응합니다. Send 우선순위는 사용하지 않습니다.</summary>
    Highest = 4,
}
