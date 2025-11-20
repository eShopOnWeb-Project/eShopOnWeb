using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.RabbitMQ.DTO;
public class CheckActiveReservationsResponse
{
    public bool success { get; set; }
    public List<int> missingItems { get; set; } = new();
}
