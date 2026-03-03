using System;
using System.Collections.Generic;

namespace service.Models;

public partial class ApplicationDatabase
{
    public decimal ApplicationId { get; set; }

    public decimal DatabaseinstanceId { get; set; }

    public string? Accesstype { get; set; }

    public virtual Application Application { get; set; } = null!;

    public virtual Databaseinstance Databaseinstance { get; set; } = null!;
}
