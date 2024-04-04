using GraphTransferLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TranslationServer
{
    internal class WebSocketCommand
    {
        [JsonProperty("command")]
        public Command Command { get; set; }

        [JsonProperty("manager_id")]
        public Guid ManagerId { get; set; }
    }
}
