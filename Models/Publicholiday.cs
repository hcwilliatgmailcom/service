using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Publicholiday
{
    public decimal Id { get; set; }

    public DateTime Date { get; set; }

    public string Name { get; set; } = null!;

    public string? LocalName { get; set; }

    public string? Type { get; set; }

    public decimal CountryId { get; set; }

    public virtual Country Country { get; set; } = null!;
}
