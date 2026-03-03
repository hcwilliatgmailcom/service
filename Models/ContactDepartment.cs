using System;
using System.Collections.Generic;

namespace service.Models;

public partial class ContactDepartment
{
    public decimal ContactId { get; set; }

    public decimal DepartmentId { get; set; }

    public string? Role { get; set; }

    public DateTime? Since { get; set; }

    public virtual Contact Contact { get; set; } = null!;

    public virtual Department Department { get; set; } = null!;
}
