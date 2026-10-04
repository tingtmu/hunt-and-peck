using System;
using System.Collections.Generic;
using HuntAndPeck.Models;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Removes redundant fallback hints. Pure; no UI Automation calls.
    /// </summary>
    internal static class HintDedup
    {
        /// <summary>
        /// Drops each candidate hint whose bounds lie inside another hint of the same window: the outer hint
        /// already covers that spot (e.g. a LegacyIAccessible hint on a part of a button). Of candidates with
        /// identical bounds the first is kept.
        /// </summary>
        /// <param name="hints">The hints, in scan order</param>
        /// <param name="isCandidate">Which hints may be dropped</param>
        /// <returns>A new list without the dropped hints, in the same order</returns>
        public static List<Hint> DropContained(IReadOnlyList<Hint> hints, Func<Hint, bool> isCandidate)
        {
            var result = new List<Hint>();
            for (var i = 0; i < hints.Count; i++)
            {
                if (!isCandidate(hints[i]) || !IsInsideAnother(hints, i, isCandidate))
                {
                    result.Add(hints[i]);
                }
            }
            return result;
        }

        private static bool IsInsideAnother(IReadOnlyList<Hint> hints, int index, Func<Hint, bool> isCandidate)
        {
            var hint = hints[index];
            for (var j = 0; j < hints.Count; j++)
            {
                var other = hints[j];
                if (j == index || other.OwningWindow != hint.OwningWindow || !other.BoundingRectangle.Contains(hint.BoundingRectangle))
                {
                    continue;
                }

                // Identical candidates would drop each other: keep the first
                var identicalCandidate = isCandidate(other) && other.BoundingRectangle == hint.BoundingRectangle;
                if (!identicalCandidate || j < index)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
