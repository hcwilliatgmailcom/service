using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Databaseoverview
{
    public decimal Id { get; set; }

    public string? Databasename { get; set; }

    public string? Engine { get; set; }

    public string? Version { get; set; }

    public decimal? Sizegb { get; set; }

    public decimal? Port { get; set; }

    public string? Servername { get; set; }

    public string? Locationname { get; set; }

    public string? City { get; set; }
}
