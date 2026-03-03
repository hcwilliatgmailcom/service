using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Country
{
    public decimal Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Capital { get; set; }

    public string? Region { get; set; }

    public string? Subregion { get; set; }

    public decimal? Population { get; set; }

    public decimal? Area { get; set; }

    public virtual ICollection<Publicholiday> Publicholidays { get; set; } = new List<Publicholiday>();
}
