using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Vendor
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Website { get; set; }

    public string? Supportphone { get; set; }

    public string? Contracttype { get; set; }

    public virtual ICollection<Application> Applications { get; set; } = new List<Application>();

    public virtual ICollection<Firewall> Firewalls { get; set; } = new List<Firewall>();

    public virtual ICollection<License> Licenses { get; set; } = new List<License>();

    public virtual ICollection<Operatingsystem> Operatingsystems { get; set; } = new List<Operatingsystem>();

    public virtual ICollection<Server> Servers { get; set; } = new List<Server>();

    public virtual ICollection<Storage> Storages { get; set; } = new List<Storage>();

    public virtual ICollection<Switch> Switches { get; set; } = new List<Switch>();

    public virtual ICollection<Widget> Widgets { get; set; } = new List<Widget>();
}
