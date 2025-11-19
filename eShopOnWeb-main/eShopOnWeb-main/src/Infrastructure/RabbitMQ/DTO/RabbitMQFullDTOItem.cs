using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.RabbitMQ.DTO;
public record RabbitMQFullDTOItem(int itemId, int total, int reserved);
