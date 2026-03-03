using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Rack
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal LocationId { get; set; }

    public string? Row { get; set; }

    public decimal? Position { get; set; }

    public decimal? Maxunits { get; set; }

    public virtual Location Location { get; set; } = null!;

    public virtual ICollection<Server> Servers { get; set; } = new List<Server>();

    public virtual ICollection<Switch> Switches { get; set; } = new List<Switch>();
}
