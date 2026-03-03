using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Environmentsummary
{
    public decimal Id { get; set; }

    public string? Environmentname { get; set; }

    public string? Description { get; set; }

    public string? Owner { get; set; }

    public string? Stage { get; set; }

    public decimal? Applicationcount { get; set; }
}
