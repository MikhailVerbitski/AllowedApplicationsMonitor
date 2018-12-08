using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace AllowedApplicationsMonitor
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Привязывает переменную AllProcessesList
        /// </summary>
        private List<Process> allProcessesList;
        public List<Process> AllProcessesList {
            get { return allProcessesList; }
            set
            {
                allProcessesList = value;
                OnPropertyChanged("AllProcessesList");
            }
        }

        private List<Process> allowedProcessesList;
        public List<Process> AllowedProcessesList {
            get { return allowedProcessesList; }
            set
            {
                allowedProcessesList = value;
                OnPropertyChanged("AllowedProcessesList");
            }
        }

        private List<string> logsList;
        public List<string> LogsList
        {
            get { return logsList; }
            set
            {
                logsList = value;
                OnPropertyChanged("LogsList");
            }
        }

        ManagementEventWatcher startWatch;
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            AllProcessesList = Process.GetProcesses().OrderBy(a => a.ProcessName).ToList();
            AllowedProcessesList = AllProcessesList;
            AllowedProcessesList.Add(Process.GetCurrentProcess());
            LogsList = new List<string>();

            startWatch = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            startWatch.EventArrived += StartProcess;
            startWatch.Start(); // всегда запускать от имени администратора

            Closed += Dispose;
        }
        public void Dispose(object sender, EventArgs e)
        {
            startWatch.Stop();
        }
        public void StartProcess(object sender, EventArrivedEventArgs e)
        {
            try
            {
                Process process = Process.GetProcessById(Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value.ToString()));
                KillProcess(process);
            }
            catch { }
        }
        public void MoveAllToAllowed(object sender, EventArgs e)
        {
            IEnumerable<Process> selected = AllList.SelectedItems.Cast<Process>();
            AllowedProcessesList = AllowedProcessesList.Concat(selected).ToList();
        }
        public void MoveAllowedToAll(object sender, EventArgs e)
        {
            IEnumerable<Process> selected = AllowedList.SelectedItems.Cast<Process>().ToArray();
            AllowedProcessesList = AllowedProcessesList.Except(selected).ToList();
            
            KillProcess(selected.ToArray());
        }

        private void KillProcess(params Process[] processes)
        {
            foreach (var item in processes)
            {
                var process = Process.GetProcessesByName(item.ProcessName).Single();
                try
                {
                    if (process != null && !AllowedProcessesList.Select(a => a.ProcessName).Contains(process.ProcessName))
                    {
                        var assessment = EvaluatorProcess.GetProcessType(process);
                        if (assessment == RM_APP_TYPE.RmExplorer
                            || assessment == RM_APP_TYPE.RmMainWindow
                            || assessment == RM_APP_TYPE.RmConsole
                            || assessment == RM_APP_TYPE.RmUnknownApp
                            || assessment == RM_APP_TYPE.RmOtherWindow)
                        {
                            process.Kill();

                            LogsList = LogsList.Concat(new List<string>() { $"Закрыт: {process.ProcessName} - {DateTime.Now}" }).ToList();
                        }
                        else
                        {
                            AllProcessesList = AllProcessesList.Concat(new List<Process>() { process }).ToList();
                        }
                    }
                }
                catch{ }
            }
        }

        /// <summary>
        /// фигня для wpf чтобы при изменении значения переменной в этом классе менялось значение на форме
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
