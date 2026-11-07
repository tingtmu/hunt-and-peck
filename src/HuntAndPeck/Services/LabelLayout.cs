using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Places hint labels so they don't cover each other. Pure; physical pixels, overlay coordinates.
    /// </summary>
    /// <remarks>
    /// A label goes at its element's top left corner, as always, unless an already placed label is there: then it
    /// slides right, else down, inside its element, else goes just left of or above the element. If nothing is
    /// free it stays at the corner. Labels of smaller elements are placed first, so a small element (a check box
    /// in a list row) keeps its corner and the larger one, with room to spare, makes way. Without collisions every
    /// label stays at its corner.
    /// </remarks>
    internal static class LabelLayout
    {
        /// <param name="elements">Each hint's element bounds</param>
        /// <param name="labelSizes">Each hint's label size before it is fitted to its element (<see cref="ActionPoint.FitLabel"/>)</param>
        /// <param name="area">Where a moved label must stay (the overlay)</param>
        /// <returns>Each label's fitted rectangle, relative to its element's top left corner</returns>
        public static IReadOnlyList<Rect> Place(IReadOnlyList<Rect> elements, IReadOnlyList<Size> labelSizes, Rect area)
        {
            var result = new Rect[elements.Count];
            var placed = new List<Rect>(elements.Count);

            // OrderBy is stable: equal areas keep their order
            var order = Enumerable.Range(0, elements.Count).OrderBy(i => AreaOf(elements[i]));
            foreach (var i in order)
            {
                var element = elements[i];
                if (element.IsEmpty)
                {
                    result[i] = Rect.Empty;
                    continue;
                }

                var size = ActionPoint.FitLabel(element.Size, labelSizes[i]);
                var label = Choose(element, size, placed, area);
                placed.Add(label);
                result[i] = new Rect(label.X - element.X, label.Y - element.Y, label.Width, label.Height);
            }
            return result;
        }

        private static Rect Choose(Rect element, Size size, List<Rect> placed, Rect area)
        {
            var corner = new Rect(element.TopLeft, size);
            if (!Collides(corner, placed))
            {
                // Today's spot, even if it pokes out of the overlay
                return corner;
            }

            var candidates = new[]
            {
                SlideRight(corner, element.Right, placed),
                SlideDown(corner, element.Bottom, placed),
                new Rect(element.Left - size.Width, element.Top, size.Width, size.Height),
                new Rect(element.Left, element.Top - size.Height, size.Width, size.Height),
            };
            foreach (var candidate in candidates)
            {
                if (candidate.HasValue && area.Contains(candidate.Value) && !Collides(candidate.Value, placed))
                {
                    return candidate.Value;
                }
            }
            return corner;
        }

        /// <summary>Moves past the labels in the way, to the right, while the label stays within <paramref name="limit"/></summary>
        private static Rect? SlideRight(Rect label, double limit, List<Rect> placed)
        {
            while (label.Right <= limit)
            {
                var blocking = placed.Where(x => Overlaps(x, label)).ToList();
                if (blocking.Count == 0)
                {
                    return label;
                }
                label.X = blocking.Max(x => x.Right);
            }
            return null;
        }

        /// <summary>Moves past the labels in the way, downwards, while the label stays within <paramref name="limit"/></summary>
        private static Rect? SlideDown(Rect label, double limit, List<Rect> placed)
        {
            while (label.Bottom <= limit)
            {
                var blocking = placed.Where(x => Overlaps(x, label)).ToList();
                if (blocking.Count == 0)
                {
                    return label;
                }
                label.Y = blocking.Max(x => x.Bottom);
            }
            return null;
        }

        private static bool Collides(Rect label, List<Rect> placed)
        {
            return placed.Any(x => Overlaps(x, label));
        }

        /// <summary>True if the rectangles share some area; touching edges don't count</summary>
        private static bool Overlaps(Rect a, Rect b)
        {
            return !a.IsEmpty && !b.IsEmpty
                && a.Left < b.Right && b.Left < a.Right
                && a.Top < b.Bottom && b.Top < a.Bottom;
        }

        private static double AreaOf(Rect rect)
        {
            return rect.IsEmpty ? 0 : rect.Width * rect.Height;
        }
    }
}
