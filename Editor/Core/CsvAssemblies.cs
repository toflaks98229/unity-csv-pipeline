using System;
using System.Reflection;

namespace CsvPipeline
{
    /// <summary>
    /// Decides which loaded assemblies discovery should look at.
    /// <para>
    /// Discovery scans every loaded assembly, and in the editor that includes <b>test assemblies</b> —
    /// this package's own among them when it is embedded or vendored. Its fixtures declare
    /// <c>[CsvAsset]</c> types pointing at tables that exist only during a test run, so a consuming
    /// project would see them listed as broken tables in the pipeline window and fail its drift check
    /// on them. Worse, the importing path would try to bake them.
    /// </para>
    /// <para>
    /// Nobody's production data lives in a test assembly, so leaving them out costs nothing.
    /// </para>
    /// </summary>
    public static class CsvAssemblies
    {
        /// <summary>Assembly names that only a test assembly references.</summary>
        private static readonly string[] TestMarkers = { "nunit.framework", "UnityEngine.TestRunner" };

        /// <summary>
        /// Whether this assembly holds tests, and so should be left out of discovery.
        /// <b>Detected by what it references</b> — Unity exposes no flag for it, and a name convention
        /// would only catch assemblies that happen to be named a certain way.
        /// </summary>
        /// <param name="assembly">Assembly to judge.</param>
        /// <returns>True when it holds tests.</returns>
        public static bool IsTestAssembly(Assembly assembly)
        {
            if (assembly == null) return false;

            AssemblyName[] references;
            try { references = assembly.GetReferencedAssemblies(); }
            catch (Exception) { return false; }   // Cannot tell, so treat it as ordinary code.

            foreach (AssemblyName reference in references)
            {
                foreach (string marker in TestMarkers)
                {
                    if (string.Equals(reference.Name, marker, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        }
    }
}
