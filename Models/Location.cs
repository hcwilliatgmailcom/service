using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Location
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public string? Type { get; set; }

    public virtual ICollection<Firewall> Firewalls { get; set; } = new List<Firewall>();

    public virtual ICollection<Network> Networks { get; set; } = new List<Network>();

    public virtual ICollection<Rack> Racks { get; set; } = new List<Rack>();

    public virtual ICollection<Server> Servers { get; set; } = new List<Server>();

    public virtual ICollection<Storage> Storages { get; set; } = new List<Storage>();

    public virtual ICollection<Switch> Switches { get; set; } = new List<Switch>();
}
