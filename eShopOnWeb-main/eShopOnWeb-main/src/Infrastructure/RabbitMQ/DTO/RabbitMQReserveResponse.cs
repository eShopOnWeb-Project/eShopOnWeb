using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.eShopWeb.Infrastructure.RabbitMQ.DTO;
public class RabbitMQReserveResponse
{
    [JsonProperty("success")]
    public bool success { get; set; }
    [JsonProperty("reason")]
    public string? reason { get; set; }
}
