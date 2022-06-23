using System;
using System.Diagnostics;
using System.Threading;

namespace CobraExternalDebugger {
	public class ProcessChecker {
		private MainWindow _mainWindow;
		private Thread _thread;
		private int _tickTime;
		private bool _keepRunning;

		public ProcessChecker(MainWindow window, int tickTime) {
			_mainWindow = window;
			this._tickTime = tickTime;
			_keepRunning = false;
			_thread = new Thread(new ThreadStart(Run));
			_thread.IsBackground = true;
		}

		private void Run() {
			while(_keepRunning) {
				string processName = _mainWindow.SelectedProcess;

				if (processName.Length > 0) {
					Process[] processes = Process.GetProcessesByName(processName);

					_mainWindow.Dispatcher.BeginInvoke(new Action(() => {
						_mainWindow.UpdateProcessRunning(processes.Length > 0);
					}));
				}

				Thread.Sleep(_tickTime);
			}
		}

		public void Start() {
			_keepRunning = true;
			if (!_thread.IsAlive) {
				_thread.Start();
			}
		}

		public void Stop() {
			_keepRunning = false;

			while (_thread.IsAlive) {
				Thread.Yield();
			}
		}
	}
}
