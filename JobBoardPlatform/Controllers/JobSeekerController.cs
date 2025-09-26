using JobBoardPlatform.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Web;
using System.Web.Mvc;
using Rotativa;

public class JobSeekerController : BaseController
{
    string conStr = ConfigurationManager.ConnectionStrings["JobBoardDB"].ConnectionString;

    public ActionResult Dashboard(string search = "", string category = "", string location = "", string sort = "", string date = "")
    {
        string dateCondition = "";
        if (date == "today")
            dateCondition = "AND CAST(J.PostedDate AS DATE) = CAST(GETDATE() AS DATE)";
        else if (date == "week")
            dateCondition = "AND J.PostedDate >= DATEADD(DAY, -7, GETDATE())";
        else if (date == "month")
            dateCondition = "AND J.PostedDate >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)";
        List<Job> jobs = new List<Job>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = $@"
    SELECT J.*, U.Name AS EmployerName FROM Jobs J 
    JOIN Users U ON J.PostedBy = U.Id 
    WHERE IsApproved = 1 
    AND (J.Title LIKE @Search OR @Search = '') 
    AND (J.Category LIKE @Category OR @Category = '') 
    AND (J.Location LIKE @Location OR @Location = '') 
    {dateCondition}";

            if (sort == "latest")
                query += " ORDER BY J.PostedDate DESC";
            else if (sort == "title_asc")
                query += " ORDER BY J.Title ASC";
            else if (sort == "title_desc")
                query += " ORDER BY J.Title DESC";

            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Search", "%" + search + "%");
            cmd.Parameters.AddWithValue("@Category", "%" + category + "%");
            cmd.Parameters.AddWithValue("@Location", "%" + location + "%");

            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                jobs.Add(new Job
                {
                    Id = (int)dr["Id"],
                    Title = dr["Title"].ToString(),
                    Description = dr["Description"].ToString(),
                    Category = dr["Category"].ToString(),
                    Location = dr["Location"].ToString(),
                    PostedDate = (DateTime)dr["PostedDate"],
                    PostedByName = dr["EmployerName"].ToString(),
                    ImagePath = dr["ImagePath"]?.ToString()
                });


            }
        }

        return View(jobs);
    }


    public ActionResult Apply(int id)
    {
        var model = new ApplyViewModel { JobId = id };
        return View(model);
    }

    [HttpPost]
    public ActionResult Apply(int jobId, HttpPostedFileBase resume)
    {
        if (Session["UserId"] == null)
            return RedirectToAction("Login", "Account");

        string resumePath = "";
        if (resume != null && resume.ContentLength > 0)
        {
            string ext = Path.GetExtension(resume.FileName).ToLower();
            if (ext != ".pdf" && ext != ".docx")
            {
                TempData["Msg"] = "Only PDF and DOCX files allowed!";
                return RedirectToAction("Dashboard");
            }

            string fileName = Guid.NewGuid().ToString() + ext;
            string serverPath = Server.MapPath("~/Resumes/");
            if (!Directory.Exists(serverPath))
                Directory.CreateDirectory(serverPath);

            resume.SaveAs(Path.Combine(serverPath, fileName));
            resumePath = "/Resumes/" + fileName;
        }

        // ✅ Insert Application
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"INSERT INTO Applications (JobId, UserId, ResumePath, AppliedDate, Status)
                         VALUES (@JobId, @UserId, @ResumePath, GETDATE(), 'Applied')";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@JobId", jobId);
            cmd.Parameters.AddWithValue("@UserId", Convert.ToInt32(Session["UserId"]));
            cmd.Parameters.AddWithValue("@ResumePath", resumePath);
            con.Open();
            cmd.ExecuteNonQuery();
        }

        // ✅ Get EmployerId + JobTitle
        int employerId = 0;
        string jobTitle = "";
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "SELECT PostedBy, Title FROM Jobs WHERE Id=@JobId";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@JobId", jobId);
            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            if (dr.Read())
            {
                employerId = (int)dr["PostedBy"];
                jobTitle = dr["Title"].ToString();
            }
        }

        // ✅ Send Notification to Employer
        // AddNotification(employerId, $"A candidate applied for your job: {jobTitle}");

        var helper = new JobBoardPlatform.Helpers.NotificationHelper();
        helper.AddNotification(employerId, $"A candidate applied for your job: {jobTitle}");

        TempData["Msg"] = "Applied with resume!";
        return RedirectToAction("Dashboard");
    }


    public ActionResult MyApplications()
    {
        int userId = Convert.ToInt32(Session["UserId"]);
        List<Application> apps = new List<Application>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"SELECT A.*, J.Title AS JobTitle, U.Name AS EmployerName
                         FROM Applications A
                         JOIN Jobs J ON A.JobId = J.Id
                         JOIN Users U ON J.PostedBy = U.Id
                         WHERE A.UserId = @UserId";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@UserId", userId);

            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                apps.Add(new Application
                {
                    Id = (int)dr["Id"],
                    JobTitle = dr["JobTitle"].ToString(),
                    EmployerName = dr["EmployerName"].ToString(),
                    AppliedDate = Convert.ToDateTime(dr["AppliedDate"]),
                    Status = dr["Status"].ToString(),
                    ResumePath = dr["ResumePath"].ToString()
                });
            }
        }

        return View(apps);
    }

    public ActionResult JobDetails(int id)
    {
        Job job = null;

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"SELECT J.*, U.Name as EmployerName FROM Jobs J
                         JOIN Users U ON J.PostedBy = U.Id
                         WHERE J.Id = @Id";

            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", id);

            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            if (dr.Read())
            {
                job = new Job
                {
                    Id = (int)dr["Id"],
                    Title = dr["Title"].ToString(),
                    Description = dr["Description"].ToString(),
                    Category = dr["Category"].ToString(),
                    Location = dr["Location"].ToString(),
                    PostedDate = (DateTime)dr["PostedDate"],
                    PostedByName = dr["EmployerName"].ToString()
                };
            }
        }

        return View(job);
    }
    public ActionResult EditProfile()
    {
        int userId = Convert.ToInt32(Session["UserId"]);
        User user = null;

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "SELECT * FROM Users WHERE Id = @Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", userId);
            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            if (dr.Read())
            {
                user = new User
                {
                    Id = (int)dr["Id"],
                    Name = dr["Name"].ToString(),
                    Email = dr["Email"].ToString(),
                    Password = dr["PasswordHash"].ToString()
                };
            }
        }

        return View(user);
    }

    [HttpPost]
    public ActionResult EditProfile(User user)
    {
        int userId = Convert.ToInt32(Session["UserId"]);

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"UPDATE Users SET Name = @Name, Email = @Email, PasswordHash = @Password 
                     WHERE Id = @Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Name", user.Name);
            cmd.Parameters.AddWithValue("@Email", user.Email);
            cmd.Parameters.AddWithValue("@Password", user.Password);
            cmd.Parameters.AddWithValue("@Id", userId);

            con.Open();
            cmd.ExecuteNonQuery();
        }

        Session["UserName"] = user.Name;
        TempData["Msg"] = "Profile updated successfully!";
        return RedirectToAction("EditProfile");
    }



    public ActionResult DownloadJobPdf(int id)
    {
        Job job = null;

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"SELECT J.*, U.Name as EmployerName FROM Jobs J
                         JOIN Users U ON J.PostedBy = U.Id
                         WHERE J.Id = @Id";

            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            if (dr.Read())
            {
                job = new Job
                {
                    Id = (int)dr["Id"],
                    Title = dr["Title"].ToString(),
                    Description = dr["Description"].ToString(),
                    Category = dr["Category"].ToString(),
                    Location = dr["Location"].ToString(),
                    PostedDate = (DateTime)dr["PostedDate"],
                    PostedByName = dr["EmployerName"].ToString(),
                    ImagePath = dr["ImagePath"]?.ToString() // ✅ FIXED
                };
            }
        }

        if (job == null)
            return HttpNotFound();

        return new Rotativa.ViewAsPdf("JobPdf", job)
        {
            FileName = $"{job.Title}_Details.pdf",
            PageSize = Rotativa.Options.Size.A4,
            PageMargins = new Rotativa.Options.Margins { Top = 20, Bottom = 20 }
        };
    }



    public ActionResult Notifications()
    {
        if (Session["UserId"] == null)
        {
            return RedirectToAction("Login", "Account");
        }

        int seekerId = Convert.ToInt32(Session["UserId"]);
        List<Notification> notifications = new List<Notification>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "SELECT * FROM Notifications WHERE UserId=@UserId ORDER BY CreatedAt DESC";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@UserId", seekerId);
            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                notifications.Add(new Notification
                {
                    Id = (int)dr["Id"],
                    Message = dr["Message"].ToString(),
                    CreatedAt = Convert.ToDateTime(dr["CreatedAt"]),
                    IsRead = Convert.ToBoolean(dr["IsRead"])
                });
            }
        }

        return View(notifications);
    }



    public ActionResult MarkNotificationAsRead(int id)
    {
        using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["JobBoardDB"].ConnectionString))
        {
            string query = "UPDATE Notifications SET IsRead = 1 WHERE Id=@Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();
            cmd.ExecuteNonQuery();
        }

        return RedirectToAction("Notifications", "JobSeeker");// seeker ka full page
    }




}
