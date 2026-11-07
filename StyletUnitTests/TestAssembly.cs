using NUnit.Framework;
using System.Threading;

// WPF 싱글턴 컨버터와 Execute의 전역 상태를 공유하므로 같은 STA에서 순차 실행합니다.
[assembly: Apartment(ApartmentState.STA)]
[assembly: NonParallelizable]
