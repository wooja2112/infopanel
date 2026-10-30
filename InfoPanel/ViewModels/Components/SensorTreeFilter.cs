using System;
using System.Collections.Generic;

namespace InfoPanel.ViewModels.Components
{
    /// <summary>
    /// Filters the sensor trees on the Design page (HWiNFO, Libre, Plugins).
    ///
    /// The filter text is split into words; an item matches when every word appears
    /// (case-insensitive) somewhere in its path, i.e. its own name or an ancestor's.
    /// So "cpu temp" finds the Temperature sensors under the CPU, and "cpu" keeps a CPU
    /// node with everything under it. Items with a matching descendant stay visible so the
    /// match can be reached.
    /// </summary>
    internal static class SensorTreeFilter
    {
        /// <summary>
        /// Updates <see cref="TreeItem.IsVisible"/> across the tree.
        /// </summary>
        /// <param name="expandMatches">
        /// True when the filter text changed: expands nodes that lead to matches, and restores the
        /// user's previous expansion when the filter is cleared. False when re-applying after new
        /// sensors were added, so the user's expand/collapse choices are left alone.
        /// </param>
        /// <returns>True if at least one item is visible.</returns>
        public static bool Apply(IEnumerable<TreeItem> roots, string? filterText, bool expandMatches)
        {
            var terms = ParseTerms(filterText);
            var anyVisible = false;

            foreach (var root in roots)
            {
                anyVisible |= Apply(root, terms, string.Empty, expandMatches);
            }

            return anyVisible;
        }

        internal static string[] ParseTerms(string? filterText)
        {
            return string.IsNullOrWhiteSpace(filterText)
                ? []
                : filterText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool Apply(TreeItem item, string[] terms, string parentPath, bool expandMatches)
        {
            var path = parentPath.Length == 0 ? item.Name : parentPath + " " + item.Name;

            var hasVisibleChild = false;
            foreach (var child in item.Children)
            {
                hasVisibleChild |= Apply(child, terms, path, expandMatches);
            }

            if (terms.Length == 0)
            {
                item.IsVisible = true;

                if (expandMatches && item.ExpandedBeforeFilter is bool wasExpanded)
                {
                    item.IsExpanded = wasExpanded;
                    item.ExpandedBeforeFilter = null;
                }

                return true;
            }

            var visible = hasVisibleChild || Matches(path, terms);
            item.IsVisible = visible;

            if (expandMatches)
            {
                item.ExpandedBeforeFilter ??= item.IsExpanded;
                item.IsExpanded = hasVisibleChild;
            }

            return visible;
        }

        private static bool Matches(string path, string[] terms)
        {
            foreach (var term in terms)
            {
                if (!path.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
