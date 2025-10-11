using JobBoardPlatform.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Web;
using System.Web.Mvc;

public class JobSeekerController : Controller
{
    string conStr = ConfigurationManager.ConnectionStrings["JobBoardDB"].ConnectionString;

    public ActionResult Dashboard(string search = "", string category = "", string location = "")
    {
        List<Job> jobs = new List<Job>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"SELECT J.*, U.Name AS EmployerName FROM Jobs J 
                         JOIN Users U ON J.PostedBy = U.Id 
                         WHERE IsApproved = 1 
                         AND (J.Title LIKE @Search OR @Search = '') 
                         AND (J.Category LIKE @Category OR @Category = '') 
                         AND (J.Location LIKE @Location OR @Location = '')";

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
                    PostedByName = dr["EmployerName"].ToString()
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


}
