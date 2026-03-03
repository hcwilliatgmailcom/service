using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Serverbylocation
{
    public string Locationname { get; set; } = null!;

    public string? City { get; set; }

    public string? Country { get; set; }

    public string? Locationtype { get; set; }

    public decimal? Servercount { get; set; }

    public decimal? Totalcpucores { get; set; }

    public decimal? Totalramgb { get; set; }
}
