using System;
using System.Collections.Generic;
using System.Linq;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Replaces registered hotkeys all-or-nothing: if any replacement fails to register, the replacements are
    /// dropped and the current hotkeys are registered again
    /// </summary>
    internal static class HotKeyTransaction
    {
        /// <param name="changes">Pairs of current hotkey (may be null) and its replacement</param>
        /// <param name="register">Registers a hotkey, returning 0 on success, else the Win32 error</param>
        /// <param name="unregister">Unregisters a hotkey if it is registered</param>
        /// <returns>
        /// Empty if all replacements are now registered; else the replacements that failed, plus (with
        /// <see cref="HotKeyFailure.IsRestore"/>) any previously registered hotkey the rollback could not restore
        /// </returns>
        public static IReadOnlyList<HotKeyFailure> Apply(
            IEnumerable<Tuple<HotKey, HotKey>> changes, Func<HotKey, int> register, Action<HotKey> unregister)
        {
            // Unchanged combinations keep their registration
            var changed = changes.Where(x => !SameCombination(x.Item1, x.Item2)).ToList();

            // Unregister all first, so hotkeys can swap combinations; remember which ones were active
            var previouslyRegistered = changed.Select(x => x.Item1).Where(x => x != null && x.IsRegistered).ToList();
            foreach (var current in previouslyRegistered)
            {
                unregister(current);
            }

            var failures = new List<HotKeyFailure>();
            var registered = new List<HotKey>();
            foreach (var replacement in changed.Select(x => x.Item2))
            {
                var error = register(replacement);
                if (error == 0)
                {
                    registered.Add(replacement);
                }
                else
                {
                    failures.Add(new HotKeyFailure(replacement, error));
                }
            }

            if (failures.Count > 0)
            {
                failures.AddRange(Rollback(previouslyRegistered, registered, register, unregister));
            }
            return failures;
        }

        /// <returns>The previously registered hotkeys that could not be registered again</returns>
        private static IEnumerable<HotKeyFailure> Rollback(
            IEnumerable<HotKey> previouslyRegistered, IEnumerable<HotKey> registered,
            Func<HotKey, int> register, Action<HotKey> unregister)
        {
            foreach (var hotKey in registered)
            {
                unregister(hotKey);
            }

            var failures = new List<HotKeyFailure>();
            foreach (var current in previouslyRegistered)
            {
                var error = register(current);
                if (error != 0)
                {
                    failures.Add(new HotKeyFailure(current, error, isRestore: true));
                }
            }
            return failures;
        }

        /// <summary>Whether both hotkeys exist and have the same key combination</summary>
        public static bool SameCombination(HotKey a, HotKey b)
        {
            return a != null && b != null && a.Modifier == b.Modifier && a.Keys == b.Keys;
        }
    }
}
