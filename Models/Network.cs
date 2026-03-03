using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Network
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal LocationId { get; set; }

    public decimal? Vlan { get; set; }

    public string? Subnet { get; set; }

    public string? Gateway { get; set; }

    public string? Type { get; set; }

    public virtual Location Location { get; set; } = null!;

    public virtual ICollection<ServerNetwork> ServerNetworks { get; set; } = new List<ServerNetwork>();
}
