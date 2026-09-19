using System;
using System.Collections.Generic;

namespace CsvPipeline
{
    /// <summary>One identifier that has already claimed its place.</summary>
    public readonly struct CsvIdClaim
    {
        /// <summary>Creates one claim.</summary>
        /// <param name="display">Spelling exactly as it first appeared.</param>
        /// <param name="line">Line number where it first appeared, or 0 when unknown.</param>
        public CsvIdClaim(string display, int line)
        {
            Display = display;
            Line = line;
        }

        /// <summary>Spelling exactly as it first appeared.</summary>
        public string Display { get; }

        /// <summary>Line number where it first appeared, or 0 when unknown.</summary>
        public int Line { get; }
    }

    /// <summary>
    /// Watches for identifiers that collide within one table.
    /// <para>
    /// An identifier becomes the output file name as-is. So when two collide, <b>both rows point at the
    /// same asset, the later row overwrites the earlier one, and the earlier row's values are gone.</b>
    /// The counts do not reveal it either — the first row counts as 'created' and the second as 'updated',
    /// which is indistinguishable from a normal import.
    /// </para>
    /// <para>
    /// <b>Names differing only by letter case count as a collision too.</b> Windows ignores letter case in
    /// file names, so <c>Sword</c> and <c>sword</c> become the same file, while on macOS and Linux they
    /// become two files. Left alone, the table <b>produces different results depending on which machine
    /// baked it</b>.
    /// </para>
    /// </summary>
    public sealed class CsvIdClaims
    {
        private readonly Dictionary<string, CsvIdClaim> _claims =
            new Dictionary<string, CsvIdClaim>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Number of distinct identifiers that have claimed a place so far.</summary>
        public int Count => _claims.Count;

        /// <summary>
        /// Tries to claim this place. It claims and returns true when the place is free, and leaves the
        /// existing claim alone and returns false when someone already holds it.
        /// </summary>
        /// <param name="key">Value that identifies the place. Usually the asset path, or the identifier when there is none.</param>
        /// <param name="display">Identifier spelling to show a person.</param>
        /// <param name="line">Line number of the current row, or 0 when unknown.</param>
        /// <param name="taken">Receives the existing claim when someone already holds the place.</param>
        /// <returns>True when this is the first claim.</returns>
        public bool TryClaim(string key, string display, int line, out CsvIdClaim taken)
        {
            if (string.IsNullOrEmpty(key))
            {
                taken = default;
                return true;   // 가릴 값이 없으면 겹침을 따지지 않습니다.
            }

            if (_claims.TryGetValue(key, out taken)) return false;

            _claims[key] = new CsvIdClaim(display, line);
            return true;
        }

        /// <summary>
        /// Turns a collision into one sentence a person can read. It lives here so the report and the
        /// preview say the same thing.
        /// </summary>
        /// <param name="display">Identifier spelling on the current row.</param>
        /// <param name="taken">The side that already held the place.</param>
        /// <returns>The explanatory sentence.</returns>
        public static string Describe(string display, CsvIdClaim taken)
        {
            string where = taken.Line > 0 ? $"row {taken.Line}" : "an earlier row";

            if (!string.Equals(display, taken.Display, StringComparison.Ordinal))
            {
                return $"Identifier '{display}' differs from '{taken.Display}' in {where} only by letter case. "
                     + "On Windows they are the same asset, so the earlier row's values are lost; on "
                     + "macOS and Linux they are two different assets. Settle on one spelling.";
            }

            return $"Identifier '{display}' was already used in {where}. "
                 + $"Both rows point at the same asset, so the later row overwrites the earlier one "
                 + $"and the values from {where} are lost.";
        }
    }
}
