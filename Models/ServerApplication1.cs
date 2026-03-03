using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Serverapplication1
{
    public string Servername { get; set; } = null!;

    public string? Serverstatus { get; set; }

    public string Applicationname { get; set; } = null!;

    public string? Version { get; set; }

    public string? Criticality { get; set; }

    public DateTime? Installeddate { get; set; }

    public string? Installstatus { get; set; }
}
