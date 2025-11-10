using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.DTOs.RabbitMQ;
public class ReserveResponse
{
    public bool success { get; set; }
    public string? reason { get; set; }
}
