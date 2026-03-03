using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Databaseinstance
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal ServerId { get; set; }

    public string? Engine { get; set; }

    public string? Version { get; set; }

    public decimal? Sizegb { get; set; }

    public decimal? Port { get; set; }

    public virtual ICollection<ApplicationDatabase> ApplicationDatabases { get; set; } = new List<ApplicationDatabase>();

    public virtual Server Server { get; set; } = null!;
}
