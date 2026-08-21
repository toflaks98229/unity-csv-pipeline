using System;

namespace CsvPipeline
{
    /// <summary>
    /// 지금 쓰이는 <see cref="ICsvAssetGateway"/>를 알려 줍니다.
    /// 기본값은 실제 Unity 구현이고, 검사는 <see cref="Use"/>로 잠시 다른 구현을 끼웁니다.
    /// </summary>
    public static class CsvAssets
    {
        private static readonly ICsvAssetGateway Default = new UnityAssetGateway();
        private static ICsvAssetGateway _current;

        /// <summary>지금 쓰이는 게이트웨이입니다.</summary>
        public static ICsvAssetGateway Current => _current ?? Default;

        /// <summary>기본 게이트웨이(실제 Unity 구현)인지 여부입니다.</summary>
        public static bool IsDefault => _current == null;

        /// <summary>
        /// 지금 쓰이는 게이트웨이와 기본 게이트웨이 모두에게 들고 있던 것을 버리라고 알립니다.
        /// 검사가 잠시 다른 구현을 끼워 둔 사이에도 <b>기본 구현의 것이 낡지 않게</b> 둘 다 부릅니다.
        /// </summary>
        public static void InvalidateCaches()
        {
            Default.InvalidateCaches();
            if (_current != null && !ReferenceEquals(_current, Default)) _current.InvalidateCaches();
        }

        /// <summary>
        /// 범위 안에서만 다른 게이트웨이를 씁니다. 반환값을 버리면 원래대로 돌아옵니다.
        /// </summary>
        /// <param name="gateway">끼울 게이트웨이입니다. null이면 기본값으로 되돌립니다.</param>
        /// <returns>범위를 닫는 핸들입니다.</returns>
        public static IDisposable Use(ICsvAssetGateway gateway) => new Scope(gateway);

        /// <summary>교체를 되돌리는 범위 핸들입니다.</summary>
        private sealed class Scope : IDisposable
        {
            private readonly ICsvAssetGateway _previous;
            private bool _closed;

            /// <summary>범위를 열고 게이트웨이를 갈아 끼웁니다.</summary>
            /// <param name="gateway">끼울 게이트웨이입니다.</param>
            public Scope(ICsvAssetGateway gateway)
            {
                _previous = _current;
                _current = gateway;
            }

            /// <summary>범위를 닫고 이전 게이트웨이로 되돌립니다.</summary>
            public void Dispose()
            {
                if (_closed) return;
                _closed = true;
                _current = _previous;
            }
        }
    }
}
