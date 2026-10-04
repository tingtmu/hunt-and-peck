using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using HuntAndPeck.NativeMethods;
using HuntAndPeck.Services;
using HuntAndPeck.Services.Interfaces;
using Xunit;

namespace HuntAndPeck.Tests.Services
{
    public class HotKeyTransactionTest
    {
        /// <summary>Combinations "owned by another app"</summary>
        private readonly HashSet<string> _taken = new HashSet<string>();

        /// <summary>Currently registered combinations</summary>
        private readonly HashSet<string> _registered = new HashSet<string>();

        [Fact]
        public void Apply_AllSucceed_RegistersReplacements()
        {
            var main = Registered(KeyModifier.Alt, Keys.OemSemicolon);
            var taskbar = Registered(KeyModifier.Control, Keys.OemSemicolon);
            var newMain = Key(KeyModifier.Alt, Keys.J);

            var failures = Apply(Tuple.Create(main, newMain), Tuple.Create(taskbar, Key(KeyModifier.Control, Keys.OemSemicolon)));

            Assert.Empty(failures);
            Assert.True(newMain.IsRegistered);
            Assert.False(main.IsRegistered);
            Assert.True(taskbar.IsRegistered); // unchanged: kept as is
            Assert.Equal(new[] { "Alt+J", "Ctrl+;" }, _registered.OrderBy(x => x));
        }

        [Fact]
        public void Apply_Swap_Succeeds()
        {
            var main = Registered(KeyModifier.Alt, Keys.OemSemicolon);
            var taskbar = Registered(KeyModifier.Control, Keys.OemSemicolon);

            var failures = Apply(
                Tuple.Create(main, Key(KeyModifier.Control, Keys.OemSemicolon)),
                Tuple.Create(taskbar, Key(KeyModifier.Alt, Keys.OemSemicolon)));

            Assert.Empty(failures);
            Assert.Equal(new[] { "Alt+;", "Ctrl+;" }, _registered.OrderBy(x => x));
        }

        [Fact]
        public void Apply_OneFails_RestoresAllCurrent()
        {
            var main = Registered(KeyModifier.Alt, Keys.OemSemicolon);
            var taskbar = Registered(KeyModifier.Control, Keys.OemSemicolon);
            var newMain = Key(KeyModifier.Alt, Keys.J);
            var newTaskbar = Key(KeyModifier.Control, Keys.K);
            _taken.Add("Ctrl+K");

            var failures = Apply(Tuple.Create(main, newMain), Tuple.Create(taskbar, newTaskbar));

            var failure = Assert.Single(failures);
            Assert.Same(newTaskbar, failure.HotKey);
            Assert.Equal(HotKeyFailure.ErrorHotKeyAlreadyRegistered, failure.ErrorCode);
            Assert.Equal("Ctrl+K is already used by another app.", failure.Message);
            Assert.False(newMain.IsRegistered);
            Assert.True(main.IsRegistered);
            Assert.True(taskbar.IsRegistered);
            Assert.Equal(new[] { "Alt+;", "Ctrl+;" }, _registered.OrderBy(x => x));
        }

        [Fact]
        public void Apply_RollbackFails_ReportsRestoreFailure()
        {
            var main = Registered(KeyModifier.Alt, Keys.OemSemicolon);
            var newMain = Key(KeyModifier.Alt, Keys.J);
            _taken.Add("Alt+J");

            // Another app grabs Alt+; while it is unregistered
            var failures = HotKeyTransaction.Apply(
                new[] { Tuple.Create(main, newMain) },
                h => { _taken.Add("Alt+;"); return Register(h); },
                Unregister);

            Assert.Equal(2, failures.Count);
            Assert.Same(newMain, failures[0].HotKey);
            Assert.False(failures[0].IsRestore);
            Assert.Same(main, failures[1].HotKey);
            Assert.True(failures[1].IsRestore);
            Assert.False(main.IsRegistered);
        }

        [Fact]
        public void Apply_RollbackSkipsHotKeysThatWereNotRegistered()
        {
            var main = Key(KeyModifier.Alt, Keys.OemSemicolon); // unavailable since startup
            var newMain = Key(KeyModifier.Alt, Keys.J);
            _taken.Add("Alt+J");

            var failures = Apply(Tuple.Create(main, newMain));

            Assert.False(Assert.Single(failures).IsRestore);
            Assert.False(main.IsRegistered);
        }

        [Fact]
        public void FailureMessage_WinCombination_MentionsWindows()
        {
            var failure = new HotKeyFailure(Key(KeyModifier.Windows, Keys.E), HotKeyFailure.ErrorHotKeyAlreadyRegistered);

            Assert.Equal("Win+E is reserved by Windows or used by another app.", failure.Message);
        }

        [Fact]
        public void Apply_NothingChanged_DoesNothing()
        {
            var main = Registered(KeyModifier.Alt, Keys.OemSemicolon);
            var calls = 0;

            var failures = HotKeyTransaction.Apply(
                new[] { Tuple.Create(main, Key(KeyModifier.Alt, Keys.OemSemicolon)) },
                h => { calls++; return 0; },
                h => calls++);

            Assert.Empty(failures);
            Assert.Equal(0, calls);
        }

        private IReadOnlyList<HotKeyFailure> Apply(params Tuple<HotKey, HotKey>[] changes)
        {
            return HotKeyTransaction.Apply(changes, Register, Unregister);
        }

        private int Register(HotKey hotKey)
        {
            var name = hotKey.ToString();
            if (_taken.Contains(name) || _registered.Contains(name))
            {
                hotKey.IsRegistered = false;
                return HotKeyFailure.ErrorHotKeyAlreadyRegistered;
            }
            _registered.Add(name);
            hotKey.IsRegistered = true;
            return 0;
        }

        private void Unregister(HotKey hotKey)
        {
            if (hotKey.IsRegistered)
            {
                _registered.Remove(hotKey.ToString());
                hotKey.IsRegistered = false;
            }
        }

        private HotKey Registered(KeyModifier modifiers, Keys key)
        {
            var hotKey = Key(modifiers, key);
            Assert.Equal(0, Register(hotKey));
            return hotKey;
        }

        private static HotKey Key(KeyModifier modifiers, Keys key)
        {
            return new HotKey { Modifier = modifiers, Keys = key };
        }
    }
}
