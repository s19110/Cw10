using Cw3.DTOs.Requests;
using Cw3.DTOs.Responses;
using Cw3.NewModels;
using Cw3.Other;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Cw3.Services
{
    public class SqlServerStudentDbService : IStudentDbService
    {

        private static SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
        

        static SqlServerStudentDbService()
        {
            builder["Data Source"] = "db-mssql";
            builder["Initial Catalog"] = "s19110";
            builder["Integrated Security"] = "true";
            builder["MultipleActiveResultSets"] = "True";
        }

       

        public EnrollStudentResponse EnrollStudent(EnrollStudentRequest request)
        {

            var db = new s19110Context();

            //1. Czy studia instnieją?
            var study = db.Studies.Where(st => st.Name.Equals(request.Studies)).FirstOrDefault();
            if (study == null)
                throw new ArgumentException("Podane studia nie instnieją");
            //3. Sprawdzenie czy student o takim indeksie już istnieje
            var student = db.Student.Where(st => st.IndexNumber == request.IndexNumber).FirstOrDefault();
            if (student != null)
                throw new ArgumentException("Student o podanym indeksie już znajduje się w bazie danych");

            //3. Szukanie w tabeli Enrollment
            var enrollment = db.Enrollment.Where(en => en.IdStudy == study.IdStudy && en.Semester == 1).FirstOrDefault();

            var toRespond = new EnrollStudentResponse();
            toRespond.LastName = request.LastName;
            var addedStudent = new NewModels.Student()
            {
                IndexNumber = request.IndexNumber,
                FirstName = request.FirstName,
                LastName = request.LastName,
                BirthDate = request.BirthDate
            };
            if(enrollment == null)
            {
                //Wstawianie nowego zapisu do bazy danych
                //Używam zagnieżdżonego selecta zamiast Identity, bo nie korzystałem z niego od początku
                var addedEnrollment = new NewModels.Enrollment()
                {
                    IdEnrollment = db.Enrollment.Max(en => en.IdEnrollment),
                    Semester = 1,
                    StartDate = DateTime.Now,
                    IdStudy = study.IdStudy,
                    IdStudyNavigation = study
                };
                db.Enrollment.Add(addedEnrollment);
                addedStudent.IdEnrollment = addedEnrollment.IdEnrollment;
                addedStudent.IdEnrollmentNavigation = addedEnrollment;
                toRespond.Semester = addedEnrollment.Semester;
                toRespond.StartDate = addedEnrollment.StartDate;
            }
            else
            {
                addedStudent.IdEnrollment = enrollment.IdEnrollment;
                addedStudent.IdEnrollmentNavigation = enrollment;
                toRespond.Semester = enrollment.Semester;
                toRespond.StartDate = enrollment.StartDate;
            }



            //4. Dodanie studenta
            db.Student.Add(addedStudent);
            db.SaveChanges();
            return toRespond;
         
        }

        public PromoteStudentResponse PromoteStudents(int semester, string studies)
        {
            var db = new s19110Context();

            var enrollment = db.Enrollment.Include(en => en.IdStudyNavigation).Where(en => en.Semester == semester && en.IdStudyNavigation.Name.Equals(studies)).FirstOrDefault();
            if (enrollment == null)
                throw new ArgumentException("Nie ma takiego wpisu");

            var par1 = new SqlParameter("@Studies", studies);
            var par2 = new SqlParameter("@semester", semester);
            db.Database.ExecuteSqlCommand("EXEC promoteStudents @Studies, @semester", par1, par2);

            return new PromoteStudentResponse
                    {
                        Semester = semester + 1,
                        Studies = studies
                    };
 


                //Zawartość procedury -- nie tworzę jej za każdym razem przy uruchamianiu tej metody

                /*Create Procedure PromoteStudents @Studies nvarchar(100), @Semester INT
     AS BEGIN
     Declare @IndexNumberCurs nvarchar(100), @NameCurs nvarchar(100), @SemesterCurs int, @StudyIdCurs int;
      Declare Studenci cursor for (Select IndexNumber, Name, Semester, Studies.IdStudy From Student
                                     Join Enrollment on Student.IdEnrollment = Enrollment.IdEnrollment join Studies on Studies.IdStudy = Enrollment.IdStudy
                                     where Name = @Studies and Semester = @Semester );

      Open studenci;
      Fetch next From studenci
      into @IndexNumberCurs, @NameCurs, @SemesterCurs, @StudyIdCurs;

      WHILE @@FETCH_STATUS = 0
         BEGIN  
         Declare @newEnrollmentId int = -1;
         Select  @newEnrollmentId = IdEnrollment from Enrollment Join Studies on Studies.IdStudy = Enrollment.IdStudy where Name = @NameCurs AND Semester=@SemesterCurs+1;


         IF @newEnrollmentId = -1
         BEGIN
         Insert Into Enrollment(IdEnrollment,Semester,IdStudy,StartDate) values ((Select Max(IdEnrollment)+1 From Enrollment), @SemesterCurs+1,@StudyIdCurs,SYSDATETIME());
         update Student set IdEnrollment =( Select MAX(IdEnrollment) FROM Enrollment )where IndexNumber=@IndexNumberCurs;
         END
         ELSE
         update Student set IdEnrollment = @newEnrollmentId where IndexNumber=@IndexNumberCurs;
         Fetch next From studenci
         into @IndexNumberCurs, @NameCurs, @SemesterCurs, @StudyIdCurs;
         END

         Close studenci;
         Deallocate studenci;
     END */


            }
        public bool CheckPassword(LoginRequestDTO request)
        {
            using (var con = new SqlConnection(builder.ConnectionString))
            using (var com = new SqlCommand())
            {
                com.Connection = con;
                con.Open();

                // Sprawdzanie haseł sprzed ich zabezpieczenia
                //   com.CommandText = "SELECT 1 FROM Student WHERE IndexNumber=@number AND Password=@Password";              
                //   com.Parameters.AddWithValue("number", request.Login);
                //  com.Parameters.AddWithValue("Password", request.Password);
                
                //    var dr = com.ExecuteReader();
                
                //   return dr.Read();

                com.CommandText = "SELECT Password,Salt FROM Student WHERE IndexNumber=@number";
                com.Parameters.AddWithValue("number", request.Login);

                using var dr = com.ExecuteReader();

                if (dr.Read())
                {
                    return SecurePassword.Validate(request.Password, dr["Salt"].ToString(), dr["Password"].ToString());
                }
                return false; //Nie ma nawet takiego indeksu w bazie danych


            }
        }

        public Claim[] GetClaims(string IndexNumber)
        {
            using (var con = new SqlConnection(builder.ConnectionString))
            using (var com = new SqlCommand())
            {
                com.Connection = con;
                con.Open();

                com.CommandText = "select Student.IndexNumber,FirstName,LastName,Role" +
                    " from Student_Roles Join Roles on Student_Roles.IdRole = Roles.IdRole join Student on Student.IndexNumber = Student_Roles.IndexNumber" +
                    " where Student.IndexNumber=@Index;";
                com.Parameters.AddWithValue("Index", IndexNumber);

                var dr = com.ExecuteReader();

                if (dr.Read())
                {
                    //Na starcie używam listy, bo nie wiem, ile ról ma dany użytkownik
                    var claimList = new List<Claim>();
                    claimList.Add(new Claim(ClaimTypes.NameIdentifier, dr["IndexNumber"].ToString()));
                    claimList.Add(new Claim(ClaimTypes.Name, dr["FirstName"].ToString() + " " + dr["LastName"].ToString())); //Nie wiem czy dawanie imienia i nazwiska w JWT to dobry pomysł, ale nie wiem jakie claimy warto utworzyć
                    claimList.Add(new Claim(ClaimTypes.Role, dr["Role"].ToString()));

                    while (dr.Read())
                    {
                        claimList.Add(new Claim(ClaimTypes.Role, dr["Role"].ToString()));
                    }
                    return claimList.ToArray<Claim>();
                }
                else return null;
                  


            }
        }

        public void SetRefreshToken(string token, string IndexNumber)
        {
            using (var con = new SqlConnection(builder.ConnectionString))
            using (var com = new SqlCommand())
            {
                com.Connection = con;
                con.Open();

                com.CommandText = "UPDATE Student SET RefreshToken=@token, TokenExpirationDate=@expires WHERE IndexNumber=@IndexNumber";
                com.Parameters.AddWithValue("token", token);
                com.Parameters.AddWithValue("expires", DateTime.Now.AddDays(2));
                com.Parameters.AddWithValue("IndexNumber", IndexNumber);

               var dr = com.ExecuteNonQuery();


            }
        }

        public string CheckRefreshToken(string token)
        {
            using (var con = new SqlConnection(builder.ConnectionString))
            using (var com = new SqlCommand())
            {
                com.Connection = con;
                con.Open();

                com.CommandText = "SELECT IndexNumber FROM STUDENT WHERE RefreshToken=@token AND TokenExpirationDate > @expires";
                com.Parameters.AddWithValue("token", token);
                com.Parameters.AddWithValue("expires", DateTime.Now);             

             using var dr = com.ExecuteReader();

                if (dr.Read())
                    return dr["IndexNumber"].ToString();
                else
                    return null;


            }
        }

        public void SetPassword(string password,string IndexNumber)
        {
            using (var con = new SqlConnection(builder.ConnectionString))
            using (var com = new SqlCommand())
            {
                com.Connection = con;
                con.Open();

                com.CommandText = "Update Student set Password=@Password, Salt=@Salt WHERE IndexNumber=@IndexNumber";
                var salt = SecurePassword.CreateSalt();
                var hashedPassword = SecurePassword.Create(password, salt);
                com.Parameters.AddWithValue("Password", hashedPassword);
                com.Parameters.AddWithValue("Salt", salt);
                com.Parameters.AddWithValue("IndexNumber", IndexNumber);

                var dr = com.ExecuteNonQuery();


            }
        }
    }
}
