using ProcessGuard.Common.Core;

namespace ProcessGuard.Common.Models
{
    public class ConfigCsvRow : ViewModelBase
    {
        private string _id;
        private string _processName;
        private string _exeFullPath;
        private string _startupParams;
        private string _onlyOpenOnce;
        private string _minimize;
        private string _noWindow;
        private string _started;
        private string _cronExpression;
        private string _stopBeforeCronExec;
        private string _error;

        public int LineNumber { get; set; }

        public string Id
        {
            get { return _id; }
            set { this.Set(ref _id, value); }
        }

        public string ProcessName
        {
            get { return _processName; }
            set { this.Set(ref _processName, value); }
        }

        public string EXEFullPath
        {
            get { return _exeFullPath; }
            set { this.Set(ref _exeFullPath, value); }
        }

        public string StartupParams
        {
            get { return _startupParams; }
            set { this.Set(ref _startupParams, value); }
        }

        public string OnlyOpenOnce
        {
            get { return _onlyOpenOnce; }
            set { this.Set(ref _onlyOpenOnce, value); }
        }

        public string Minimize
        {
            get { return _minimize; }
            set { this.Set(ref _minimize, value); }
        }

        public string NoWindow
        {
            get { return _noWindow; }
            set { this.Set(ref _noWindow, value); }
        }

        public string Started
        {
            get { return _started; }
            set { this.Set(ref _started, value); }
        }

        public string CronExpression
        {
            get { return _cronExpression; }
            set { this.Set(ref _cronExpression, value); }
        }

        public string StopBeforeCronExec
        {
            get { return _stopBeforeCronExec; }
            set { this.Set(ref _stopBeforeCronExec, value); }
        }

        public string Error
        {
            get { return _error; }
            set { this.Set(ref _error, value); }
        }
    }
}
