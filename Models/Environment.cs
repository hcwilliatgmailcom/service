using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Environment
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? Owner { get; set; }

    public string? Stage { get; set; }

    public virtual ICollection<Application> Applications { get; set; } = new List<Application>();
}
