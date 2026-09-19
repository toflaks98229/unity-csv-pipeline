using System;

namespace CsvPipeline
{
    /// <summary>
    /// Tells you which <see cref="ICsvAssetGateway"/> is in use right now.
    /// The default is the real Unity implementation; tests swap another one in for a while with <see cref="Use"/>.
    /// </summary>
    public static class CsvAssets
    {
        private static readonly ICsvAssetGateway Default = new UnityAssetGateway();
        private static ICsvAssetGateway _current;

        /// <summary>The gateway in use right now.</summary>
        public static ICsvAssetGateway Current => _current ?? Default;

        /// <summary>Whether the default gateway (the real Unity implementation) is in use.</summary>
        public static bool IsDefault => _current == null;

        /// <summary>
        /// Tells both the gateway in use and the default gateway to throw away what they hold.
        /// It calls both so that <b>the default implementation's caches do not go stale</b> while a test has
        /// another implementation swapped in.
        /// </summary>
        public static void InvalidateCaches()
        {
            Default.InvalidateCaches();
            if (_current != null && !ReferenceEquals(_current, Default)) _current.InvalidateCaches();
        }

        /// <summary>
        /// Uses a different gateway inside the scope only. Disposing the return value restores the previous one.
        /// </summary>
        /// <param name="gateway">Gateway to swap in. Null restores the default.</param>
        /// <returns>A handle that closes the scope.</returns>
        public static IDisposable Use(ICsvAssetGateway gateway) => new Scope(gateway);

        /// <summary>The scope handle that undoes the swap.</summary>
        private sealed class Scope : IDisposable
        {
            private readonly ICsvAssetGateway _previous;
            private bool _closed;

            /// <summary>Opens the scope and swaps the gateway in.</summary>
            /// <param name="gateway">Gateway to swap in.</param>
            public Scope(ICsvAssetGateway gateway)
            {
                _previous = _current;
                _current = gateway;
            }

            /// <summary>Closes the scope and restores the previous gateway.</summary>
            public void Dispose()
            {
                if (_closed) return;
                _closed = true;
                _current = _previous;
            }
        }
    }
}
