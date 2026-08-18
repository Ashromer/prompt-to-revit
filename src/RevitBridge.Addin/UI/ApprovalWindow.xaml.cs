using System;
using System.Windows;
using System.Windows.Threading;

namespace RevitBridge.Addin.UI
{
    public partial class ApprovalWindow : Window
    {
        private DispatcherTimer _timer;
        private int _timeLeft = 60;
        public bool IsApproved { get; private set; } = false;
        
        public event EventHandler WindowClosedResult;

        public ApprovalWindow(string snippet)
        {
            InitializeComponent();
            CodeTextBox.Text = snippet;
            
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
            
            UpdateTimerText();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _timeLeft--;
            UpdateTimerText();
            
            if (_timeLeft <= 0)
            {
                _timer.Stop();
                IsApproved = false;
                CloseWindow();
            }
        }

        private void UpdateTimerText()
        {
            TimerTextBlock.Text = $"Se rechazará automáticamente en {_timeLeft}s";
        }

        private void ApproveButton_Click(object sender, RoutedEventArgs e)
        {
            IsApproved = true;
            CloseWindow();
        }

        private void RejectButton_Click(object sender, RoutedEventArgs e)
        {
            IsApproved = false;
            CloseWindow();
        }
        
        private void CloseWindow()
        {
            if (_timer != null)
            {
                _timer.Stop();
            }
            WindowClosedResult?.Invoke(this, EventArgs.Empty);
            this.Close();
        }
        
        protected override void OnClosed(EventArgs e)
        {
            if (_timer != null)
            {
                _timer.Stop();
            }
            base.OnClosed(e);
        }
    }
}
