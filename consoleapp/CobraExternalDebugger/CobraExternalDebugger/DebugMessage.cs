using Newtonsoft.Json;

namespace CobraExternalDebugger {
	internal class DebugMessage {

		[JsonProperty("Type")]
		public string Type { get; set; }

		[JsonProperty("Body")]
		public string Body { get; set; }
	}
}
