using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace CobraExternalDebugger {
	public partial class MainWindow : Window {
		public struct MessageFormat {
			private string _colour;
			private FontWeight _weight;
			private FontStyle _style;

			public string Colour {
				get { return _colour; }
				set { _colour = value; }
			}

			public FontWeight Weight {
				get { return _weight; }
				set { _weight = value; }
			}

			public FontStyle Style {
				get { return _style; }
				set { _style = value; }
			}
		}

		private enum MessageType {
			Standard,
			Warning,
			Error,
			PlayerCommand,
			ConsoleSuccess,
			ConsoleFailure,
			Count
		};

		private Dictionary<MessageType, MessageFormat> _messageTypeFormats = new Dictionary<MessageType, MessageFormat>();
		private Dictionary<Key, bool> _ignorableKeys = new Dictionary<Key, bool>();
		Configuration _config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

		private Server _server;
		private ProcessChecker _processChecker;
		private List<string> _commands;
		private List<string> _pendingCommands;
		private int _currentCommand;
		private bool _isScrollEnabled;
		private bool _isProcessRunning;
		private bool _isClearConsoleEnabled;
		private bool _isClearCommandsEnabled;
		private bool _isApplicationSet = false;
		private string _selectedProcess;
		private string _selectedApplication;
		private string _baseAddress = "http://localhost";
		private bool _isCtrlDown;

		public MainWindow() {
			Console.WriteLine("ApplicationIndex :: " + Convert.ToInt32(_config.AppSettings.Settings["Application"].Value));
			InitializeComponent();
			_isProcessRunning = false;
			_isCtrlDown = false;

			_messageTypeFormats[MessageType.Standard] = new MessageFormat { Colour = "#e4e7ed", Weight = FontWeights.Normal, Style = FontStyles.Normal };
			_messageTypeFormats[MessageType.Warning] = new MessageFormat { Colour = "#e2e629", Weight = FontWeights.Normal, Style = FontStyles.Italic };
			_messageTypeFormats[MessageType.Error] = new MessageFormat { Colour = "#ff2929", Weight = FontWeights.Normal, Style = FontStyles.Italic };
			_messageTypeFormats[MessageType.PlayerCommand] = new MessageFormat { Colour = "#62b0f0", Weight = FontWeights.Bold, Style = FontStyles.Normal };
			_messageTypeFormats[MessageType.ConsoleSuccess] = new MessageFormat { Colour = "#68e85f", Weight = FontWeights.Bold, Style = FontStyles.Italic };
			_messageTypeFormats[MessageType.ConsoleFailure] = new MessageFormat { Colour = "#d86be8", Weight = FontWeights.Bold, Style = FontStyles.Italic };

			_ignorableKeys[Key.LeftCtrl] = true;
			_ignorableKeys[Key.RightCtrl] = true;
			_ignorableKeys[Key.LeftShift] = true;
			_ignorableKeys[Key.RightShift] = true;
			_ignorableKeys[Key.LeftAlt] = true;
			_ignorableKeys[Key.RightAlt] = true;
			_ignorableKeys[Key.LWin] = true;
			_ignorableKeys[Key.RWin] = true;
			_ignorableKeys[Key.Delete] = true;
			_ignorableKeys[Key.PageUp] = true;
			_ignorableKeys[Key.PageDown] = true;
			_ignorableKeys[Key.Home] = true;
			_ignorableKeys[Key.End] = true;
			_ignorableKeys[Key.Scroll] = true;
			_ignorableKeys[Key.CapsLock] = true;
			_ignorableKeys[Key.NumLock] = true;
			_ignorableKeys[Key.Pause] = true;
			_ignorableKeys[Key.Print] = true;
			_ignorableKeys[Key.PrintScreen] = true;
			_ignorableKeys[Key.Insert] = true;

			for (Key i = Key.F1; i <= Key.F24; i++) {
				_ignorableKeys[i] = true;
			}

			Console.WriteLine("ApplicationIndex :: " + Convert.ToInt32(_config.AppSettings.Settings["Application"].Value));
			ApplicationDropdown.SelectedIndex = Convert.ToInt32(_config.AppSettings.Settings["Application"].Value);
			ExportText.Text = _config.AppSettings.Settings["ExportPath"].Value;
			_isClearConsoleEnabled = Convert.ToBoolean(_config.AppSettings.Settings["ClearConsole"].Value);
			_isClearCommandsEnabled = Convert.ToBoolean(_config.AppSettings.Settings["ClearCommandCache"].Value);
			_isScrollEnabled = Convert.ToBoolean(_config.AppSettings.Settings["AutoScroll"].Value);
			PortInput.Text = _config.AppSettings.Settings["Port"].Value;

			ClearConsoleCheckbox.IsChecked = _isClearConsoleEnabled;
			ClearCommandsCheckbox.IsChecked = _isClearCommandsEnabled;
			AutoScrollCheckbox.IsChecked = _isScrollEnabled;

			_isApplicationSet = true;

			InputText.Focusable = true;
			InputText.Focus();

			_commands = new List<string>();
			_pendingCommands = new List<string>();
			_currentCommand = 0;

			_server = new Server(this, GenerateAddress(_baseAddress, PortInput.Text));
			_server.Start();
			TryAppendMessage("Server \"" + _server.Address + "\" Started.", "ConsoleSuccess");

			_processChecker = new ProcessChecker(this, 100);
			_processChecker.Start();

			RefreshMaximizeRestoreButton();
		}

		public List<string> PendingCommands {
			get { return _pendingCommands; }
		}

		public string SelectedProcess {
			get { return _selectedProcess; }
		}

		public string SelectedApplication {
			get { return _selectedApplication; }
		}

		public void ClearPendingCommands() {
			_pendingCommands.Clear();
		}

		public void ClearSavedCommands() {
			_commands.Clear();
			_currentCommand = 0;
		}

		private void ClearConsole() {
			TextRange textRange = new TextRange(ConsoleText.Document.ContentStart, ConsoleText.Document.ContentEnd);
			textRange.Text = "";
		}

		private void SetCurrentCommandToLast() {
			_currentCommand = _commands.Count;
		}

		public void UpdateProcessName() {
			var selectedItem = ApplicationDropdown.SelectedItem;

			if (selectedItem != null) {
				_selectedProcess = ((ComboBoxItem)selectedItem).Tag.ToString();
			}

			_selectedProcess = "";
		}

		public void UpdateApplicationName() {
			var selectedItem = ApplicationDropdown.SelectedItem;

			if (selectedItem != null) {
				_selectedApplication = ((ComboBoxItem)selectedItem).Content.ToString();
			}

			_selectedApplication = "";
		}

		public void UpdateProcessRunning(bool isProcessRunning) {
			if (!_isProcessRunning && isProcessRunning) {
				if (_isClearConsoleEnabled) {
					ClearConsole();
				}

				if (_isClearCommandsEnabled) {
					ClearSavedCommands();
				}

				TryAppendMessage("Application launched: " + _selectedApplication, "ConsoleSuccess");
			}
			else if (_isProcessRunning && !isProcessRunning) {
				TryAppendMessage("Application closed: " + _selectedApplication, "ConsoleFailure");
			}

			_isProcessRunning = isProcessRunning;
		}
		private void UpdateConfigSetting(string setting, string value) {
			_config.AppSettings.Settings[setting].Value = value;
			_config.Save(ConfigurationSaveMode.Modified);
			ConfigurationManager.RefreshSection("appSettings");
		}

		private string ProcessMessageForConsole(string message) {
			string processedMessage = "";

			TextRange textRange = new TextRange(ConsoleText.Document.ContentStart, ConsoleText.Document.ContentEnd);
			if (textRange.Text.Length > 0) {
				processedMessage = message + Environment.NewLine;
			}
			else {
				processedMessage = message + Environment.NewLine + Environment.NewLine;
			}

			return processedMessage;
		}

		public string GenerateAddress(string baseAddress, string port) {
			return baseAddress + ":" + port + "/";
		}



		// ---------------------------------------------------
		// -------------------- Messaging --------------------
		// ---------------------------------------------------

		public void TryAppendMessage(string message, string type) {
			string processedMessage = ProcessMessageForConsole(message);

			switch (type.ToLower()) {
				case "standard":
					AppendMessage(processedMessage, MessageType.Standard);
					break;
				case "warning":
					AppendMessage(processedMessage, MessageType.Warning);
					break;
				case "error":
					AppendMessage(processedMessage, MessageType.Error);
					break;
				case "playercommand":
					AppendMessage(processedMessage, MessageType.PlayerCommand);
					break;
				case "consolesuccess":
					AppendMessage(processedMessage, MessageType.ConsoleSuccess);
					break;
				case "consolefailure":
					AppendMessage(processedMessage, MessageType.ConsoleFailure);
					break;
				default:
					break;
			}
		}

		private void AppendMessage(string message, MessageType messageType) {
			BrushConverter brushConverter = new BrushConverter();
			TextRange textRange = new TextRange(ConsoleText.Document.ContentEnd, ConsoleText.Document.ContentEnd);
			textRange.Text = message;
			var colour = brushConverter.ConvertFromString(_messageTypeFormats[messageType].Colour);

			try {
				textRange.ApplyPropertyValue(TextElement.ForegroundProperty, colour);
				textRange.ApplyPropertyValue(TextElement.FontWeightProperty, _messageTypeFormats[messageType].Weight);
				textRange.ApplyPropertyValue(TextElement.FontStyleProperty, _messageTypeFormats[messageType].Style);
			}
			catch (FormatException) {
				Console.WriteLine("Format Exception!");
			}

			if (_isScrollEnabled) {
				ConsoleText.ScrollToEnd();
			}
		}

		public bool ShouldSnapIgnoreKey(Key key) {
			return (_ignorableKeys.ContainsKey(key));
		}



		// ----------------------------------------------------------------------
		// -------------------- Event Handlers :: Console UI --------------------
		// ----------------------------------------------------------------------

		protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e) {
			if (!ExportText.IsFocused && !PortInput.IsFocused && !_isCtrlDown && !ShouldSnapIgnoreKey(e.Key)) {
				InputText.Focus();

				if (e.Key == Key.Up && _commands.Count > 0) {
					if (_currentCommand > 0) {
						_currentCommand--;
					}

					InputText.Text = _commands[_currentCommand];
				}
				else if (e.Key == Key.Down) {
					_currentCommand++;


					if (_currentCommand >= _commands.Count) {
						SetCurrentCommandToLast();
						InputText.Text = "";
					}
					else {
						InputText.Text = _commands[_currentCommand];
					}
				}
			}
		}

		protected override void OnKeyDown(KeyEventArgs e) {
			if (!ExportText.IsFocused && !PortInput.IsFocused && !_isCtrlDown && !ShouldSnapIgnoreKey(e.Key)) {
				if (e.Key == Key.Escape) {
					InputText.Clear();
					SetCurrentCommandToLast();
				}

				else if (e.Key == Key.Return && InputText.Text.Length > 0) {
					string command = InputText.Text;
					_commands.Add(command);
					_pendingCommands.Add(command);
					SetCurrentCommandToLast();

					if (command == "clear") {
						ClearConsole();
						InputText.Clear();
					}
					else {
						TryAppendMessage("> " + command, "PlayerCommand");
						InputText.Clear();
					}
				}
			}

			if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) {
				_isCtrlDown = true;
			}
		}

		protected override void OnKeyUp(KeyEventArgs e) {
			if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) {
				_isCtrlDown = false;
			}
		}

		void OnWindowClose(object sender, CancelEventArgs e) {
			_server.Stop();
			_processChecker.Stop();
		}

		private void OnClick_RestartServer(object sender, RoutedEventArgs e) {
			string port = PortInput.Text;

			_server.Stop();
			TryAppendMessage("Server \"" + _server.Address +"\" Stopped.", "ConsoleFailure");
			_server.Address = GenerateAddress(_baseAddress, PortInput.Text);
			_server.Start();
			TryAppendMessage("Server \"" + _server.Address + "\" Started.", "ConsoleSuccess");

			UpdateConfigSetting("Port", port);
		}

		private void OnClick_ExportTo(object sender, RoutedEventArgs e) {
			TextRange textRange = new TextRange(ConsoleText.Document.ContentStart, ConsoleText.Document.ContentEnd);
			string text = textRange.Text;

			SaveFileDialog dialog = new SaveFileDialog() {
				Filter = "Text Files(*.txt)|*.txt|Lua Scripts(*.lua)|*.lua|All(*.*)|*"
			};

			if (dialog.ShowDialog() == true) {
				File.WriteAllText(dialog.FileName, text);
				ExportText.Text = dialog.FileName;

				UpdateConfigSetting("ExportPath", dialog.FileName);
				TryAppendMessage("Export Successful.", "ConsoleSuccess");
			}
		}

		private void OnClick_Export(object sender, RoutedEventArgs e) {
			TextRange textRange = new TextRange(ConsoleText.Document.ContentStart, ConsoleText.Document.ContentEnd);
			string text = textRange.Text;
			string fileName = ExportText.Text;

			try {
				File.WriteAllText(fileName, text);

				UpdateConfigSetting("ExportPath", fileName);
				TryAppendMessage("Export Successful.", "ConsoleSuccess");
			}
			catch (Exception ex) {
				MessageBox.Show("Unable to save file at the specified location (" + ex.Message + ").", "Save error", MessageBoxButton.OK, MessageBoxImage.Error);
				TryAppendMessage("Export Unsuccessful.", "ConsoleFailure");
			}
		}

		private void OnClick_ClearConsole(object sender, RoutedEventArgs e) {
			ClearConsole();
		}

		private void OnClick_ClearCommands(object sender, RoutedEventArgs e) {
			ClearSavedCommands();
		}

		private void OnSelectionChanged_ApplicationDropdown(object sender, SelectionChangedEventArgs e) {
			_selectedProcess = (e.AddedItems[0] as ComboBoxItem).Tag.ToString();
			_selectedApplication = (e.AddedItems[0] as ComboBoxItem).Content.ToString();

			if (_isApplicationSet) {
				UpdateConfigSetting("Application", Convert.ToString(ApplicationDropdown.SelectedIndex));
			}
		}

		private void OnCheck_ClearConsoleCheckbox(object sender, RoutedEventArgs e) {
			_isClearConsoleEnabled = (ClearConsoleCheckbox.IsChecked ?? false);

			UpdateConfigSetting("ClearConsole", Convert.ToString(ClearConsoleCheckbox.IsChecked));
		}

		private void OnCheck_ClearCommandsCheckbox(object sender, RoutedEventArgs e) {
			_isClearCommandsEnabled = (ClearCommandsCheckbox.IsChecked ?? false);

			UpdateConfigSetting("ClearCommandCache", Convert.ToString(ClearCommandsCheckbox.IsChecked));
		}

		private void OnCheck_AutoScrollCheckbox(object sender, RoutedEventArgs e) {
			_isScrollEnabled = (AutoScrollCheckbox.IsChecked ?? false);
			Console.WriteLine("IsScrollEnabled :: " + _isScrollEnabled);

			UpdateConfigSetting("AutoScroll", Convert.ToString(AutoScrollCheckbox.IsChecked));
		}



		// ---------------------------------------------------------------------
		// -------------------- Event Handlers :: Title Bar --------------------
		// ---------------------------------------------------------------------

		private void OnMinimizeButtonClick(object sender, RoutedEventArgs e) {
			WindowState = WindowState.Minimized;
		}

		private void OnMaximizeRestoreButtonClick(object sender, RoutedEventArgs e) {
			if (WindowState == WindowState.Maximized) {
				WindowState = WindowState.Normal;
			}
			else {
				WindowState = WindowState.Maximized;
			}
		}

		private void OnCloseButtonClick(object sender, RoutedEventArgs e) {
			this.Close();
		}

		private void RefreshMaximizeRestoreButton() {
			if (WindowState == WindowState.Maximized) {
				maximizeButton.Visibility = Visibility.Collapsed;
				restoreButton.Visibility = Visibility.Visible;
			}
			else {
				maximizeButton.Visibility = Visibility.Visible;
				restoreButton.Visibility = Visibility.Collapsed;
			}
		}

		private void Window_StateChanged(object sender, EventArgs e) {
			RefreshMaximizeRestoreButton();
		}
	}
}
