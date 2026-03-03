using System;
using System.Collections.Generic;

namespace service.Models;

public partial class Contact
{
    public decimal Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Role { get; set; }

    public decimal DepartmentId { get; set; }

    public virtual ICollection<ContactDepartment> ContactDepartments { get; set; } = new List<ContactDepartment>();

    public virtual Department Department { get; set; } = null!;
}
