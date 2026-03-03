using System;
using System.Collections.Generic;

namespace service.Models;

public partial class ServerNetwork
{
    public decimal ServerId { get; set; }

    public decimal NetworkId { get; set; }

    public string? Ipaddress { get; set; }

    public string? Macaddress { get; set; }

    public virtual Network Network { get; set; } = null!;

    public virtual Server Server { get; set; } = null!;
}
