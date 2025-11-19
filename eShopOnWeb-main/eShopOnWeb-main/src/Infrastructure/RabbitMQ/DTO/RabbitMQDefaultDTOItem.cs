using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.eShopWeb.Infrastructure.RabbitMQ.DTO;
public class RabbitMQDefaultDTOItem
{
    [JsonProperty("itemId")]
    public int itemId { get; set; }
    [JsonProperty("amount")]
    public int amount { get; set; }
}
