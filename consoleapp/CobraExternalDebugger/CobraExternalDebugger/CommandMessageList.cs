using System.Collections.Generic;
using Newtonsoft.Json;

namespace CobraExternalDebugger {
	public class CommandMessageList {

		[JsonProperty("Commands")]
		public List<string> Commands { get; set; }
	}
}
