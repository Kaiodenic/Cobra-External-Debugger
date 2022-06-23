using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using Newtonsoft.Json;

namespace CobraExternalDebugger {
	internal class Server {
		private MainWindow _mainWindow;
		private HttpListener _listener;
		private string _address;

		public Server(MainWindow window, string address) {
			if (!HttpListener.IsSupported) {
				throw new NotSupportedException("System not supported.");
			}

			_mainWindow = window;
			_address = address;
		}

		public string Address {
			get { return _address; }
			set { _address = value; }
		}

		private string ProcessPost(Dictionary<string, string> headers, Dictionary<string, string> requestTypes, string body) {
			try {
				if (headers.ContainsKey("Content-Type")) {
					if (headers["Content-Type"] == "application/json" && requestTypes["request"] == "addmessage") {
						Dictionary<string, DebugMessage> messages = JsonConvert.DeserializeObject<Dictionary<string, DebugMessage>>(body);

						foreach (DebugMessage message in messages.Values) {
							if (message != null) {
								_mainWindow.Dispatcher.BeginInvoke(new Action(() => {
									_mainWindow.TryAppendMessage(message.Body, message.Type);
								}));
							}
						}
					}
				}
			}
			catch (Exception) {
				Console.WriteLine("Error :: Exception thrown while processing Post request!");
			}

			return "";
		}

		private string ProcessGet(Dictionary<string, string> requestTypes) {
			if (requestTypes["request"] == "getcommands") {
				string jsonString = "{}";
				List<string> pendingCommands = _mainWindow.PendingCommands;

				if (pendingCommands.Count > 0) {
					var jsonList = new CommandMessageList { Commands = pendingCommands };
					jsonString = JsonConvert.SerializeObject(jsonList);
					_mainWindow.ClearPendingCommands();
				}

				return jsonString;
			}

			return "";
		}

		private void ProcessRequest(HttpListenerContext context) {
			// Get the data from the HTTP stream
			HttpListenerRequest request = context.Request;

			string body = new StreamReader(request.InputStream).ReadToEnd();

			string response = "";
			Dictionary<string, string> requestTypes = new Dictionary<string, string>();
			Dictionary<string, string> headers = new Dictionary<string, string>();

			var rawURL = request.RawUrl.Split('?');
			var Path = rawURL[0];

			var rawParams = rawURL[1];
			if (rawParams != null) {
				var splitParams = rawParams.Split('&');

				foreach (var param in splitParams) {
					var values = param.Split('=');

					if (values.Length > 1) {
						requestTypes[values[0]] = values[1];
					}
					else if (values.Length > 0) {
						requestTypes[values[0]] = "";
					}
				}
			}

			var rawHeaders = request.Headers;
			foreach (string key in rawHeaders.AllKeys) {
				string[] values = rawHeaders.GetValues(key);

				if (values.Length > 0) {
					headers[key] = values[0];
				}
			}

			if (headers.ContainsKey("User-Agent") && requestTypes.ContainsKey("request")) {
				if (headers["User-Agent"] == "cobra-external-debugger") {
					if (request.HttpMethod == "POST") {
						string tempResponse = ProcessPost(headers, requestTypes, body);

						if (tempResponse.Length > 0) {
							response = tempResponse;
						}
					}
					else if (request.HttpMethod == "GET") {
						string tempResponse = ProcessGet(requestTypes);

						if (tempResponse.Length > 0) {
							response = tempResponse;
						}
					}
				}
			}

			byte[] responseData = Encoding.UTF8.GetBytes(response);
			context.Response.StatusCode = 200;
			context.Response.KeepAlive = false;
			context.Response.ContentLength64 = responseData.Length;

			Stream outputStream = context.Response.OutputStream;
			outputStream.Write(responseData, 0, responseData.Length);
			context.Response.Close();
		}

		public void Run() {
			while (_listener.IsListening) {
				try {
					HttpListenerContext context = _listener.GetContext();

					ThreadPool.QueueUserWorkItem(
						(c) => {
							var ctx = c as HttpListenerContext;
							ProcessRequest(ctx);

						},
						context
					);
				}
				catch (System.Net.HttpListenerException) {
					Console.WriteLine("Warning :: Listener Stopped.");
				}
				catch (Exception ex) {
					MessageBox.Show("The server \"" + _address + "\" stopped unexpectedly (" + ex.Message + ").", "Server Stopped Unexpectedly", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		}

		public void Start() {
			_listener = new HttpListener();
			_listener.Prefixes.Add(_address);

			try {
				_listener.Start();
			}
			catch (Exception ex) {
				MessageBox.Show("Unable to start server with address \"" + _address + "\" (" + ex.Message + ").", "Server Start Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}

			ThreadPool.QueueUserWorkItem((o) => {
				Run();
			});
		}

		public void Stop() {
			if (_listener.IsListening) {
				_listener.Stop();
			}
			_listener.Close();
		}
	}
}
