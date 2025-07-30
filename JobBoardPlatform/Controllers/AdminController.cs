using JobBoardPlatform.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;
using System.Drawing;
using System.IO;

public class AdminController : Controller
{
    string conStr = ConfigurationManager.ConnectionStrings["JobBoardDB"].ConnectionString;

    // ✅ 1. Admin Dashboard - Show Unapproved Jobs
    public ActionResult Dashboard()
    {



        if (Session["UserRole"]?.ToString() != "Admin")
            return RedirectToAction("Login", "Account");

        var model = new AdminDashboardViewModel();
        model.PendingJobs = new List<Job>();

        using (SqlConnection con = new SqlConnection(conStr))
        {

            con.Open();

            // Total Jobs
            using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Jobs", con))
                model.TotalJobs = (int)cmd.ExecuteScalar();

            // Pending Jobs Count
            using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Jobs WHERE IsApproved = 0", con))
                model.PendingJobsCount = (int)cmd.ExecuteScalar();

            // Total Applications
            using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Applications", con))
                model.TotalApplications = (int)cmd.ExecuteScalar();

            // Approved Applications
            using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Applications WHERE Status = 'Approved'", con))
                model.ApprovedApplications = (int)cmd.ExecuteScalar();

            // Rejected Applications
            using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Applications WHERE Status = 'Rejected'", con))
                model.RejectedApplications = (int)cmd.ExecuteScalar();

            // Total Employers
            using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Role = 'Employer'", con))
                model.TotalEmployers = (int)cmd.ExecuteScalar();

            // List of Pending Jobs
            using (SqlCommand cmd = new SqlCommand("SELECT * FROM Jobs WHERE IsApproved = 0", con))
            {
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        model.PendingJobs.Add(new Job
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            Title = reader["Title"].ToString(),
                            Description = reader["Description"].ToString(),
                            PostedDate = Convert.ToDateTime(reader["PostedDate"])
                        });
                    }
                }
            }
        }

        return View(model);
    }




    // ✅ 2. Approve Job
    public ActionResult ApproveJob(int id)
    {
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "UPDATE Jobs SET IsApproved = 1 WHERE Id = @Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();
            cmd.ExecuteNonQuery();
        }
        return RedirectToAction("Dashboard");
    }

    // ✅ 3. Reject Job
    public ActionResult RejectJob(int id)
    {
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "DELETE FROM Jobs WHERE Id = @Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();
            cmd.ExecuteNonQuery();
        }
        return RedirectToAction("Dashboard");
    }

    // ✅ 4. View New Applications Only (Status = Applied)
    public ActionResult Applications()
    {
        List<Application> apps = new List<Application>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"SELECT A.Id, A.AppliedDate, A.Status, A.ResumePath, 
                             J.Title AS JobTitle, U.Name AS SeekerName, E.Name AS EmployerName
                             FROM Applications A
                             JOIN Jobs J ON A.JobId = J.Id
                             JOIN Users U ON A.UserId = U.Id
                             JOIN Users E ON J.PostedBy = E.Id
                             WHERE A.Status = 'Applied'";

            SqlCommand cmd = new SqlCommand(query, con);
            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                apps.Add(new Application
                {
                    Id = (int)dr["Id"],
                    AppliedDate = (DateTime)dr["AppliedDate"],
                    Status = dr["Status"].ToString(),
                    ResumePath = dr["ResumePath"].ToString(),
                    CandidateName = dr["SeekerName"].ToString(),
                    JobTitle = dr["JobTitle"].ToString(),
                    EmployerName = dr["EmployerName"].ToString()
                });
            }
        }

        return View(apps); // View: Views/Admin/Applications.cshtml
    }

    // ✅ 5. View All Applications (No filter)
    public ActionResult ViewApplications(string searchTerm = "", string statusFilter = "")
    {
        List<Application> apps = new List<Application>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"
        SELECT A.Id, A.AppliedDate, A.Status, A.ResumePath, 
               J.Title AS JobTitle, 
               E.Name AS EmployerName, 
               U.Name AS CandidateName
        FROM Applications A
        JOIN Jobs J ON A.JobId = J.Id
        JOIN Users U ON A.UserId = U.Id
        JOIN Users E ON J.PostedBy = E.Id
        WHERE 1 = 1";

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query += " AND (U.Name LIKE @Search OR J.Title LIKE @Search)";
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                query += " AND A.Status = @Status";
            }

            SqlCommand cmd = new SqlCommand(query, con);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                cmd.Parameters.AddWithValue("@Search", "%" + searchTerm + "%");
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                cmd.Parameters.AddWithValue("@Status", statusFilter);
            }

            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                apps.Add(new Application
                {
                    Id = (int)dr["Id"],
                    AppliedDate = (DateTime)dr["AppliedDate"],
                    Status = dr["Status"].ToString(),
                    ResumePath = dr["ResumePath"].ToString(),
                    JobTitle = dr["JobTitle"].ToString(),
                    EmployerName = dr["EmployerName"].ToString(),
                    CandidateName = dr["CandidateName"].ToString()
                });
            }
        }

        // For keeping search & filter values in the view
        ViewBag.SearchTerm = searchTerm;
        ViewBag.StatusFilter = statusFilter;

        return View(apps); // View: Views/Admin/ViewApplications.cshtml
    }

    public ActionResult DownloadJobsExcel()
    {
        List<Job> jobs = new List<Job>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "SELECT * FROM Jobs WHERE IsApproved = 1";
            SqlCommand cmd = new SqlCommand(query, con);
            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                jobs.Add(new Job
                {
                    Title = dr["Title"].ToString(),
                    Category = dr["Category"].ToString(),
                    Location = dr["Location"].ToString(),
                    Description = dr["Description"].ToString(),
                    PostedDate = Convert.ToDateTime(dr["PostedDate"])
                });
            }
        }

        // ✅ Set EPPlus license context for v7
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using (ExcelPackage package = new ExcelPackage())
        {
            var ws = package.Workbook.Worksheets.Add("Jobs");

            // Header row
            ws.Cells[1, 1].Value = "Title";
            ws.Cells[1, 2].Value = "Category";
            ws.Cells[1, 3].Value = "Location";
            ws.Cells[1, 4].Value = "Description";
            ws.Cells[1, 5].Value = "Posted Date";

            using (var range = ws.Cells[1, 1, 1, 5])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            }

            // Data rows
            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                ws.Cells[i + 2, 1].Value = job.Title;
                ws.Cells[i + 2, 2].Value = job.Category;
                ws.Cells[i + 2, 3].Value = job.Location;
                ws.Cells[i + 2, 4].Value = job.Description;
                ws.Cells[i + 2, 5].Value = job.PostedDate.ToString("dd-MM-yyyy");
            }

            ws.Cells.AutoFitColumns();

            var fileBytes = package.GetAsByteArray();
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "AllJobs.xlsx");
        }
    }

    public ActionResult DownloadApplicationsExcel()
    {
        List<dynamic> applications = new List<dynamic>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"
            SELECT A.Id, J.Title AS JobTitle, U.Name AS CandidateName, A.AppliedDate, A.Status
            FROM Applications A
            JOIN Jobs J ON A.JobId = J.Id
            JOIN Users U ON A.UserId = U.Id";

            SqlCommand cmd = new SqlCommand(query, con);
            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                applications.Add(new
                {
                    Id = Convert.ToInt32(dr["Id"]),
                    JobTitle = dr["JobTitle"].ToString(),
                    CandidateName = dr["CandidateName"].ToString(),
                    AppliedDate = Convert.ToDateTime(dr["AppliedDate"]),
                    Status = dr["Status"].ToString()
                });
            }
        }

        // ✅ Set EPPlus license context
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using (ExcelPackage package = new ExcelPackage())
        {
            var ws = package.Workbook.Worksheets.Add("Applications");

            // Header
            ws.Cells[1, 1].Value = "Application ID";
            ws.Cells[1, 2].Value = "Job Title";
            ws.Cells[1, 3].Value = "Candidate Name";
            ws.Cells[1, 4].Value = "Applied Date";
            ws.Cells[1, 5].Value = "Status";

            using (var range = ws.Cells[1, 1, 1, 5])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            }

            // Data
            for (int i = 0; i < applications.Count; i++)
            {
                var app = applications[i];
                ws.Cells[i + 2, 1].Value = app.Id;
                ws.Cells[i + 2, 2].Value = app.JobTitle;
                ws.Cells[i + 2, 3].Value = app.CandidateName;
                ws.Cells[i + 2, 4].Value = app.AppliedDate.ToString("dd-MM-yyyy");
                ws.Cells[i + 2, 5].Value = app.Status;
            }

            ws.Cells.AutoFitColumns();

            var fileBytes = package.GetAsByteArray();
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "AllApplications.xlsx");
        }
    }

    public ActionResult ViewEmployers()
    {
        List<User> employers = new List<User>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "SELECT * FROM Users WHERE Role = 'Employer'";
            SqlCommand cmd = new SqlCommand(query, con);
            con.Open();

            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                employers.Add(new User
                {
                    Id = (int)reader["Id"],
                    Name = reader["Name"].ToString(),
                    Email = reader["Email"].ToString(),
                    Role = reader["Role"].ToString(),
                    IsBlocked = (bool)reader["IsBlocked"]
                });
            }
        }

        return View(employers);
    }

    public ActionResult BlockEmployer(int id)
    {
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "UPDATE Users SET IsBlocked = 1 WHERE Id = @Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();
            cmd.ExecuteNonQuery();
        }
        return RedirectToAction("ViewEmployers");
    }

    public ActionResult UnblockEmployer(int id)
    {
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "UPDATE Users SET IsBlocked = 0 WHERE Id = @Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();
            cmd.ExecuteNonQuery();
        }
        return RedirectToAction("ViewEmployers");
    }

    public ActionResult ManageJobSeekers()
    {
        List<User> seekers = new List<User>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "SELECT Id, Name, Email, IsBlocked FROM Users WHERE Role = 'JobSeeker'";
            SqlCommand cmd = new SqlCommand(query, con);
            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                seekers.Add(new User
                {
                    Id = Convert.ToInt32(dr["Id"]),
                    Name = dr["Name"].ToString(),
                    Email = dr["Email"].ToString(),
                    IsBlocked = Convert.ToBoolean(dr["IsBlocked"])
                });
            }
        }

        return View(seekers);
    }

    public ActionResult BlockUser(int id)
    {
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "UPDATE Users SET IsBlocked = 1 WHERE Id = @Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();
            cmd.ExecuteNonQuery();
        }

        return RedirectToAction("ManageJobSeekers"); // You can update this to redirect to appropriate page
    }

    public ActionResult UnblockUser(int id)
    {
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "UPDATE Users SET IsBlocked = 0 WHERE Id = @Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();
            cmd.ExecuteNonQuery();
        }

        return RedirectToAction("ManageJobSeekers"); // or ManageEmployers based on context
    }


    public ActionResult ManageJobs()
    {
        List<JobWithEmployerViewModel> jobs = new List<JobWithEmployerViewModel>();
        string connectionString = ConfigurationManager.ConnectionStrings["JobBoardDB"].ConnectionString;


        using (SqlConnection con = new SqlConnection(connectionString))
        {
            string query = @"
            SELECT J.Id, J.Title, J.Description, J.Category, J.Location, J.PostedDate, J.IsApproved,
                   U.Name AS EmployerName
            FROM Jobs J
            JOIN Users U ON J.PostedBy = U.Id
        ";

            SqlCommand cmd = new SqlCommand(query, con);
            con.Open();
            SqlDataReader rdr = cmd.ExecuteReader();

            while (rdr.Read())
            {
                jobs.Add(new JobWithEmployerViewModel
                {
                    Id = Convert.ToInt32(rdr["Id"]),
                    Title = rdr["Title"].ToString(),
                    Description = rdr["Description"].ToString(),
                    Category = rdr["Category"].ToString(),
                    Location = rdr["Location"].ToString(),
                    PostedDate = Convert.ToDateTime(rdr["PostedDate"]),
                    IsApproved = Convert.ToBoolean(rdr["IsApproved"]),
                    EmployerName = rdr["EmployerName"].ToString()
                });
            }
        }

        return View(jobs);
    }



}
