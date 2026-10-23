using System;
using System.Collections.Generic;
using System.Linq;
using HuntAndPeck.Models;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Orders hints the way a page is read, so labels follow the screen instead of the UI Automation tree
    /// </summary>
    internal static class HintOrdering
    {
        /// <summary>
        /// Rows top to bottom, each left to right. A hint joins the current row if its top is within half the
        /// median hint height of the row's first hint, so slightly misaligned controls still share a row.
        /// </summary>
        /// <returns>A new list; the input is not changed. Hints with equal positions keep their order.</returns>
        public static List<Hint> ByReadingOrder(IList<Hint> hints)
        {
            if (hints.Count < 2)
            {
                return hints.ToList();
            }

            var tolerance = MedianHeight(hints) / 2;
            var byTop = hints.OrderBy(x => x.BoundingRectangle.Top).ToList();

            var result = new List<Hint>(hints.Count);
            var rowStart = 0;
            for (var i = 1; i <= byTop.Count; i++)
            {
                if (i == byTop.Count || byTop[i].BoundingRectangle.Top - byTop[rowStart].BoundingRectangle.Top > tolerance)
                {
                    result.AddRange(byTop.Skip(rowStart).Take(i - rowStart).OrderBy(x => x.BoundingRectangle.Left));
                    rowStart = i;
                }
            }
            return result;
        }

        private static double MedianHeight(IList<Hint> hints)
        {
            var heights = hints.Select(x => x.BoundingRectangle.Height).OrderBy(x => x).ToList();
            return Math.Max(0, heights[heights.Count / 2]);
        }
    }
}
