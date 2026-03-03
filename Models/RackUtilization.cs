using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Rackutilization
{
    public decimal Id { get; set; }

    public string? Rackname { get; set; }

    public string? Row { get; set; }

    public decimal? Position { get; set; }

    public decimal? Maxunits { get; set; }

    public string? Locationname { get; set; }

    public string? City { get; set; }

    public decimal? Servercount { get; set; }
}
