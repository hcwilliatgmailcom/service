using System;
using System.Collections.Generic;

namespace service.Models;

public partial class ServerApplication
{
    public decimal ServerId { get; set; }

    public decimal ApplicationId { get; set; }

    public DateTime? Installeddate { get; set; }

    public string? Status { get; set; }

    public virtual Application Application { get; set; } = null!;

    public virtual Server Server { get; set; } = null!;
}
