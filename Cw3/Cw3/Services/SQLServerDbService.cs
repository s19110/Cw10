using Cw3.Exceptions;
using Cw3.NewModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace Cw3.DAL
{
    public class SQLServerDbService : IDbService
    {
        private static SqlConnectionStringBuilder builder;
        static SQLServerDbService()
        {
            builder = new SqlConnectionStringBuilder();
            builder["Data Source"] = "db-mssql";
            builder["Initial Catalog"] = "s19110";
            builder["Integrated Security"] = "true";
            
        }
        public IEnumerable<NewModels.Student> GetStudents()
        {
            var Db = new s19110Context();
            return Db.Student.ToList();
        }

        public NewModels.Enrollment GetEnrollment(String idStudenta)
        {
            var Db = new s19110Context();
            var student = Db.Student.Where(st => st.IndexNumber.Equals(idStudenta)).FirstOrDefault();
            if (student == null)
                throw new StudentNotFoundException("Nie ma takiego studenta");
            return Db.Enrollment.Where(e => e.IdEnrollment.Equals(student.IdEnrollment)).FirstOrDefault();
        }

        //Korzystam z innego modelu studenta, bo nie chcę, żeby można było podawać sól itp.
        public void ModifyStudent(Models.Student newData)
        {
            var Db = new s19110Context();
            var toUpdate = Db.Student.Where(st => st.IndexNumber == newData.IndexNumber).FirstOrDefault();
            if (toUpdate == null)
                throw new StudentNotFoundException("Nie ma takiego studenta");
            toUpdate.FirstName = newData.FirstName != null ? newData.FirstName : toUpdate.FirstName;
            toUpdate.LastName = newData.LastName != null ? newData.LastName : toUpdate.LastName;
            toUpdate.BirthDate = newData.BirthDate.Equals(null) ? newData.BirthDate : toUpdate.BirthDate;


            if (newData.Studies != null && newData.Semester != 0)
            {
                var enrollment = Db.Enrollment.Where(en => en.IdStudyNavigation.Name.Equals(newData.Studies) && en.Semester == newData.Semester).FirstOrDefault();
                if (enrollment != null)
                {
                    toUpdate.IdEnrollment = enrollment.IdEnrollment;
                    toUpdate.IdEnrollmentNavigation = enrollment;
                }
                else
                {
                    var study = Db.Studies.Where(st => st.Name.Equals(newData.Studies)).FirstOrDefault();
                    if (study == null)
                        throw new ArgumentException("Podany typ studiów nie istnieje");
                    var newEnrollment = new NewModels.Enrollment()
                    {
                        IdStudy = study.IdStudy,
                        IdStudyNavigation = study,
                        Semester = newData.Semester
                    };
                    Db.Enrollment.Add(newEnrollment);
                    toUpdate.IdEnrollmentNavigation = newEnrollment;
                }
           
            }
            
            Db.SaveChanges();
        }

        public void DeleteStudent(string IndexNumber)
        {
            var Db = new s19110Context();
            var deletedRoles = Db.StudentRoles.Where(sr => sr.IndexNumber.Equals(IndexNumber)).ToList();

            var deleted = new NewModels.Student()
            {
                IndexNumber = IndexNumber
            };
          
            foreach (var sr in deletedRoles) {
                Db.Remove(sr);
            }
            Db.SaveChanges(); //Muszę jakoś obejść więzy spójności przy rolach z poprzednich zajęć

            Db.Remove(deleted);
            Db.SaveChanges();

        }

        IEnumerable<Models.Student> IDbService.GetStudents()
        {
            var Db = new s19110Context();
            var dbStudents = Db.Student.Include(st => st.IdEnrollmentNavigation).Include(st => st.IdEnrollmentNavigation.IdStudyNavigation).ToList();

            var modelStudents = new List<Models.Student>();

            foreach(var student in dbStudents)
            {
                var toAdd = new Models.Student()
                {
                    FirstName = student.FirstName,
                    LastName = student.LastName,
                    IndexNumber = student.IndexNumber,
                    BirthDate = student.BirthDate
                };             
                 
                if(student.IdEnrollmentNavigation != null)
                {
                    toAdd.Studies = student.IdEnrollmentNavigation.IdStudyNavigation.Name;
                    toAdd.Semester = student.IdEnrollmentNavigation.Semester;
                }
                modelStudents.Add(toAdd);
            }

            return modelStudents;
        }

        Models.Student IDbService.GetStudent(string IndexNumber)
        {
            var Db = new s19110Context();
            var student = Db.Student.Where(st => st.IndexNumber.Equals(IndexNumber)).FirstOrDefault();
            if (student == null)
                throw new StudentNotFoundException("Nie ma takiego studenta");
            return new Models.Student() 
            {
                FirstName = student.FirstName,
                LastName = student.LastName,
                IndexNumber = student.IndexNumber,
                BirthDate = student.BirthDate,
                Semester = student.IdEnrollmentNavigation.Semester,
                Studies = student.IdEnrollmentNavigation.IdStudyNavigation.Name
            };
        }
    }
}
