using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Applicationdependency
{
    public string Applicationname { get; set; } = null!;

    public string? Appversion { get; set; }

    public string? Criticality { get; set; }

    public string Databasename { get; set; } = null!;

    public string? Engine { get; set; }

    public string? Dbversion { get; set; }

    public decimal? Sizegb { get; set; }

    public string? Accesstype { get; set; }
}
