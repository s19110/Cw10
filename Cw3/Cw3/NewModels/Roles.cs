using System;
using System.Collections.Generic;

namespace Cw3.NewModels
{
    public partial class Roles
    {
        public Roles()
        {
            StudentRoles = new HashSet<StudentRoles>();
        }

        public int IdRole { get; set; }
        public string Role { get; set; }

        public virtual ICollection<StudentRoles> StudentRoles { get; set; }
    }
}
