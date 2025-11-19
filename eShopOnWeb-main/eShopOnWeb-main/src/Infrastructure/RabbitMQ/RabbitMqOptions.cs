using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.RabbitMQ;
public class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public int Port { get; set; } = 5672;
}
