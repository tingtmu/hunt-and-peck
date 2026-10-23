using System;
using System.Windows.Threading;

namespace HuntAndPeck.Views
{
    /// <summary>
    /// Shows a message over a window for a moment, without taking the foreground or the keyboard
    /// </summary>
    public partial class NoticeView
    {
        private static readonly TimeSpan ShowFor = TimeSpan.FromMilliseconds(1200);

        public NoticeView()
        {
            InitializeComponent();
            OverlayTheme.ForSystem().ApplyTo(Resources);

            var timer = new DispatcherTimer { Interval = ShowFor };
            timer.Tick += (sender, e) =>
            {
                timer.Stop();
                Close();
            };
            Loaded += (sender, e) => timer.Start();
        }

        protected override bool TakesForeground => false;
    }
}
