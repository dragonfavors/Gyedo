# ![프로젝트 아이콘](StyletIcon.png) Stylet.MvvmToolkit

[Stylet](https://github.com/canton7/Stylet)을 기반으로, 화면 관리 기능을 유지하면서 구조와 사용 방식을 단순화하는 포크입니다.

## 원본 Stylet

Stylet은 Antony Male이 만든 WPF용 ViewModel 중심 MVVM 프레임워크입니다. Caliburn.Micro에서 영향을 받았으며, 테스트와 확장이 쉬운 애플리케이션을 만드는 것을 목표로 합니다.

주요 기능은 다음과 같습니다.

- `Screen`: 화면의 활성화·비활성화·종료와 수명주기 관리
- `Conductor`: 여러 화면의 관리와 탐색
- `ViewManager`, `View.Model`: ViewModel에 대응하는 View 탐색·생성·연결
- `WindowManager`: 창, 대화상자, 메시지 박스 표시
- 속성 변경 알림, 데이터 검증, 메시징, 자체 IoC 컨테이너
- XAML Action을 통한 ViewModel 메서드 호출

원본 자료:

- [소스 코드](https://github.com/canton7/Stylet)
- [문서 및 사용 방법](https://github.com/canton7/Stylet/wiki)
- [NuGet 패키지](https://www.nuget.org/packages/Stylet/)

## 이 저장소의 방향

**기존 화면 관리 기능을 유지하고, 프레임워크의 책임과 사용 방식을 단순화합니다.**

그 첫 단계로 XAML Action 계열과 `View.ActionTarget`을 제거했습니다. 메서드 이름을 해석하고 호출 대상을 추적하던 자체 연결 계층을 줄이고, WPF의 명시적인 명령 바인딩과 이벤트 처리를 사용합니다.

CommunityToolkit.Mvvm 통합은 계획 중이며, 아직 적용되지 않았습니다. Action 제거는 단순화를 위한 작업입니다.

## 원본과의 차이

아래 비교는 이 저장소가 기반으로 삼은 원본 코드와 현재 코드를 기준으로 합니다.

| 항목 | 원본 Stylet | 이 저장소 |
|---|---|---|
| 화면 수명주기와 탐색 | `Screen`, `Conductor` 사용 | 유지 |
| View 탐색·생성·연결 | `ViewManager`, `View.Model` 사용 | 유지 |
| 창·대화상자 관리 | `WindowManager` 사용 | 유지 |
| 속성 알림·검증·메시징·IoC | Stylet 자체 구현 사용 | 유지 |
| 명령 연결 | `{s:Action Save}`로 메서드 호출 가능 | `ICommand` 속성에 일반 WPF 바인딩 사용 |
| 명령 실행 조건 | Action에서 `CanSave` 같은 이름 규칙 해석 | 명령의 `CanExecute`와 `CanExecuteChanged`로 명시 |
| 이벤트 연결 | Action으로 이벤트를 메서드에 연결 가능 | View 이벤트 처리기 등으로 명시 |
| 호출 대상 지정 | `View.ActionTarget` 제공 | 제거 |
| 기본 메시지 박스 | Action으로 버튼 클릭 처리 | View의 버튼 이벤트로 처리 |
| 샘플 | Action 사용 예제 포함 | 일반 명령 바인딩과 이벤트 처리로 수정 |
| README | 영어 | 한국어로 원본과 변경 사항 안내 |

## 기존 코드 사용 시 차이

Action 제거는 기존 사용 코드와 호환되지 않는 변경입니다. 다음처럼 명령 속성에 바인딩해야 합니다.

```xml
<!-- 원본의 Action 사용 방식 -->
<Button Command="{s:Action Save}" />

<!-- 이 저장소의 명령 바인딩 방식 -->
<Button Command="{Binding SaveCommand}" />
```

`SaveCommand`의 구현과 실행 조건 갱신은 애플리케이션에서 담당합니다. [샘플의 DelegateCommand](Samples/Common/DelegateCommand.cs)는 예제와 테스트용이며, 라이브러리 API로 제공하지 않습니다.

원본 문서의 화면 관리 설명은 참고할 수 있지만, Action과 `View.ActionTarget` 예제는 이 저장소에 적용되지 않습니다. 원본 NuGet 패키지에도 이 포크의 변경 사항이 포함된 것으로 간주해서는 안 됩니다.

현재 사용 예제는 [Samples](Samples), 변경 이력은 [CHANGELOG.txt](CHANGELOG.txt)를 참고하세요.

## 라이선스와 저작권

원본과 동일한 [MIT 라이선스](LICENSE.txt)를 따릅니다.

- 원본 저작권: Copyright (c) 2014 Antony Male
- 이 저장소의 추가 저작권: Copyright (c) 2026 dragonfavors

원본의 라이선스와 저작권 고지를 유지합니다.
