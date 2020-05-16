using System;
using System.Collections.Generic;

namespace Cw3.NewModels
{
    public partial class Student
    {
        public Student()
        {
            StudentRoles = new HashSet<StudentRoles>();
        }

        public string IndexNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime BirthDate { get; set; }
        public int IdEnrollment { get; set; }
        public string Password { get; set; }
        public string RefreshToken { get; set; }
        public DateTime? TokenExpirationDate { get; set; }
        public string Salt { get; set; }

        public virtual Enrollment IdEnrollmentNavigation { get; set; }
        public virtual ICollection<StudentRoles> StudentRoles { get; set; }
    }
}
