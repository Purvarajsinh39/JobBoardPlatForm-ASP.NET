using JobBoardPlatform.Models;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Web;
using System.Web.Mvc;
using OfficeOpenXml.Style;
using System.Drawing;

public class EmployerController : Controller
{
    string conStr = ConfigurationManager.ConnectionStrings["JobBoardDB"].ConnectionString;

    public ActionResult Dashboard(string searchTerm = "")
    {
        if (Session["UserId"] != null)
        {
            int userId = Convert.ToInt32(Session["UserId"]);

            using (SqlConnection con = new SqlConnection(conStr))
            {
                string query = "SELECT IsBlocked FROM Users WHERE Id = @Id";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Id", userId);
                con.Open();

                bool isBlocked = Convert.ToBoolean(cmd.ExecuteScalar());
                if (isBlocked)
                {
                    return RedirectToAction("Blocked", "Account");
                }
            }
        }

        int empId = Convert.ToInt32(Session["UserId"]);
        List<Job> jobs = new List<Job>();
        int totalApplications = 0, approvedJobs = 0, pendingApps = 0, approvedApps = 0, rejectedApps = 0;

        using (SqlConnection con = new SqlConnection(conStr))
        {

            con.Open();

            string jobQuery = @"SELECT * FROM Jobs 
                                WHERE PostedBy=@PostedBy
                                AND (Title LIKE @Search OR Category LIKE @Search)";
            SqlCommand jobCmd = new SqlCommand(jobQuery, con);
            jobCmd.Parameters.AddWithValue("@PostedBy", empId);
            jobCmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");

            SqlDataReader dr = jobCmd.ExecuteReader();
            while (dr.Read())
            {
                var jobId = (int)dr["Id"];
                bool isApproved = (bool)dr["IsApproved"];
                if (isApproved) approvedJobs++;

                jobs.Add(new Job
                {
                    Id = jobId,
                    Title = dr["Title"].ToString(),
                    Description = dr["Description"].ToString(),
                    Category = dr["Category"].ToString(),
                    Location = dr["Location"].ToString(),
                    PostedDate = (DateTime)dr["PostedDate"],
                    IsApproved = isApproved,
                    ImagePath = dr["ImagePath"]?.ToString(),
                    ApplicationCount = 0
                });
            }
            dr.Close();

            foreach (var job in jobs)
            {
                string countQuery = "SELECT COUNT(*) FROM Applications WHERE JobId = @JobId";
                SqlCommand countCmd = new SqlCommand(countQuery, con);
                countCmd.Parameters.AddWithValue("@JobId", job.Id);
                job.ApplicationCount = (int)countCmd.ExecuteScalar();
            }

            string statQuery = @"SELECT Status, COUNT(*) AS Count
                                 FROM Applications A
                                 JOIN Jobs J ON A.JobId = J.Id
                                 WHERE J.PostedBy = @PostedBy
                                 GROUP BY Status";

            SqlCommand statCmd = new SqlCommand(statQuery, con);
            statCmd.Parameters.AddWithValue("@PostedBy", empId);
            SqlDataReader statDr = statCmd.ExecuteReader();

            while (statDr.Read())
            {
                string status = statDr["Status"].ToString();
                int count = (int)statDr["Count"];

                totalApplications += count;
                if (status == "Applied") pendingApps = count;
                else if (status == "Approved") approvedApps = count;
                else if (status == "Rejected") rejectedApps = count;
            }
        }

        ViewBag.TotalJobs = jobs.Count;
        ViewBag.ApprovedJobs = approvedJobs;
        ViewBag.TotalApplications = totalApplications;
        ViewBag.PendingApplications = pendingApps;
        ViewBag.ApprovedApplications = approvedApps;
        ViewBag.RejectedApplications = rejectedApps;
        ViewBag.SearchTerm = searchTerm;

        return View(jobs);
    }

    public ActionResult CreateJob() => View();

    [HttpPost]
    public ActionResult PostJob(Job job, HttpPostedFileBase ImageFile)
    {
        if (Session["UserId"] != null)
        {
            int userId = Convert.ToInt32(Session["UserId"]);

            using (SqlConnection con = new SqlConnection(conStr))
            {
                string query = "SELECT IsBlocked FROM Users WHERE Id = @Id";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Id", userId);
                con.Open();

                bool isBlocked = Convert.ToBoolean(cmd.ExecuteScalar());
                if (isBlocked)
                {
                    return RedirectToAction("Blocked", "Account");
                }
            }
        }

        if (ImageFile != null && ImageFile.ContentLength > 0)
        {
            var fileName = Path.GetFileName(ImageFile.FileName);
            var uploadDir = Server.MapPath("~/Uploads");

            if (!Directory.Exists(uploadDir))
                Directory.CreateDirectory(uploadDir);

            var filePath = Path.Combine(uploadDir, fileName);
            ImageFile.SaveAs(filePath);

            job.ImagePath = "/Uploads/" + fileName;
        }

        job.PostedBy = Convert.ToInt32(Session["UserId"]);
        job.PostedDate = DateTime.Now;
        job.IsApproved = false;

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"INSERT INTO Jobs 
                            (Title, Description, Category, Location, PostedBy, PostedDate, IsApproved, ImagePath)
                             VALUES 
                            (@Title, @Description, @Category, @Location, @PostedBy, @PostedDate, @IsApproved, @ImagePath)";

            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Title", job.Title);
            cmd.Parameters.AddWithValue("@Description", job.Description);
            cmd.Parameters.AddWithValue("@Category", job.Category);
            cmd.Parameters.AddWithValue("@Location", job.Location);
            cmd.Parameters.AddWithValue("@PostedBy", job.PostedBy);
            cmd.Parameters.AddWithValue("@PostedDate", job.PostedDate);
            cmd.Parameters.AddWithValue("@IsApproved", job.IsApproved);
            cmd.Parameters.AddWithValue("@ImagePath", job.ImagePath ?? (object)DBNull.Value);

            con.Open();
            cmd.ExecuteNonQuery();
        }

        return RedirectToAction("Dashboard");
    }

    public ActionResult EditJob(int id)
    {
        Job job = new Job();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "SELECT * FROM Jobs WHERE Id = @Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            if (dr.Read())
            {
                job.Id = (int)dr["Id"];
                job.Title = dr["Title"].ToString();
                job.Description = dr["Description"].ToString();
                job.Category = dr["Category"].ToString();
                job.Location = dr["Location"].ToString();
                job.ImagePath = dr["ImagePath"]?.ToString();
            }
        }

        return View(job);
    }


    [HttpPost]
    public ActionResult Edit(Job job, HttpPostedFileBase ImageFile)
    {
        if (ModelState.IsValid)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["JobBoardDB"].ConnectionString))
            {
                con.Open();

                string imagePath = job.ImagePath; // default = old image path

                if (ImageFile != null && ImageFile.ContentLength > 0)
                {
                    string fileName = Path.GetFileName(ImageFile.FileName);
                    string imageFolder = "~/Uploads/";
                    string physicalPath = Server.MapPath(imageFolder + fileName);
                    ImageFile.SaveAs(physicalPath);
                    imagePath = imageFolder + fileName;
                }

                string query = "UPDATE Jobs SET Title=@Title, Description=@Description, Category=@Category, Location=@Location, ImagePath=@ImagePath WHERE Id=@Id";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Title", job.Title);
                cmd.Parameters.AddWithValue("@Description", job.Description);
                cmd.Parameters.AddWithValue("@Category", job.Category);
                cmd.Parameters.AddWithValue("@Location", job.Location);
                cmd.Parameters.AddWithValue("@ImagePath", imagePath);
                cmd.Parameters.AddWithValue("@Id", job.Id);
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("Dashboard", "Employer");
        }

        return View(job);
    }


    public ActionResult JobDetails(int id)
    {
        Job job = null;

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "SELECT * FROM Jobs WHERE Id = @Id";
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
                    IsApproved = (bool)dr["IsApproved"],
                    ImagePath = dr["ImagePath"]?.ToString()
                };
            }
        }

        if (job == null)
            return HttpNotFound();

        return View(job);
    }

    public ActionResult DeleteJob(int id)
    {
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "DELETE FROM Jobs WHERE Id = @Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();
            cmd.ExecuteNonQuery();
        }

        TempData["SuccessMessage"] = "Job deleted successfully!";
        return RedirectToAction("Dashboard");
    }

    public ActionResult ViewApplications(int id, string searchTerm, string statusFilter)
    {
        if (Session["UserId"] != null)
        {
            int userId = Convert.ToInt32(Session["UserId"]);

            using (SqlConnection con = new SqlConnection(conStr))
            {
                string query = "SELECT IsBlocked FROM Users WHERE Id = @Id";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Id", userId);
                con.Open();

                bool isBlocked = Convert.ToBoolean(cmd.ExecuteScalar());
                if (isBlocked)
                {
                    return RedirectToAction("Blocked", "Account");
                }
            }
        }

        ViewBag.JobId = id;

        List<Application> applications = new List<Application>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"SELECT A.*, U.Name AS CandidateName, J.Title AS JobTitle
                             FROM Applications A
                             JOIN Users U ON A.UserId = U.Id
                             JOIN Jobs J ON A.JobId = J.Id
                             WHERE A.JobId = @JobId";

            if (!string.IsNullOrEmpty(searchTerm))
                query += " AND (U.Name LIKE @Search OR J.Title LIKE @Search)";
            if (!string.IsNullOrEmpty(statusFilter))
                query += " AND A.Status = @StatusFilter";

            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@JobId", id);

            if (!string.IsNullOrEmpty(searchTerm))
                cmd.Parameters.AddWithValue("@Search", "%" + searchTerm + "%");
            if (!string.IsNullOrEmpty(statusFilter))
                cmd.Parameters.AddWithValue("@StatusFilter", statusFilter);

            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                applications.Add(new Application
                {
                    Id = (int)dr["Id"],
                    JobId = (int)dr["JobId"],
                    UserId = (int)dr["UserId"],
                    ResumePath = dr["ResumePath"].ToString(),
                    AppliedDate = (DateTime)dr["AppliedDate"],
                    Status = dr["Status"].ToString(),
                    CandidateName = dr["CandidateName"].ToString(),
                    JobTitle = dr["JobTitle"].ToString()
                });
            }
        }

        return View(applications);
    }

    public ActionResult ApproveApplication(int id, int jobId)
    {
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "UPDATE Applications SET Status = 'Approved' WHERE Id = @Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();
            cmd.ExecuteNonQuery();
        }
        return RedirectToAction("ViewApplications", new { id = jobId });
    }

    public ActionResult RejectApplication(int id, int jobId)
    {
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "UPDATE Applications SET Status = 'Rejected' WHERE Id = @Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();
            cmd.ExecuteNonQuery();
        }
        return RedirectToAction("ViewApplications", new { id = jobId });
    }

    public ActionResult ShortlistedCandidates(string searchTerm = "")
    {
        int empId = Convert.ToInt32(Session["UserId"]);
        List<Application> applications = new List<Application>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"
                SELECT A.*, U.Name AS CandidateName, J.Title AS JobTitle
                FROM Applications A
                JOIN Users U ON A.UserId = U.Id
                JOIN Jobs J ON A.JobId = J.Id
                WHERE A.Status = 'Approved' AND J.PostedBy = @PostedBy
                  AND (U.Name LIKE @Search OR J.Title LIKE @Search)";

            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@PostedBy", empId);
            cmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");

            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                applications.Add(new Application
                {
                    Id = (int)dr["Id"],
                    JobId = (int)dr["JobId"],
                    UserId = (int)dr["UserId"],
                    ResumePath = dr["ResumePath"].ToString(),
                    AppliedDate = (DateTime)dr["AppliedDate"],
                    Status = dr["Status"].ToString(),
                    CandidateName = dr["CandidateName"].ToString(),
                    JobTitle = dr["JobTitle"].ToString()
                });
            }
        }

        ViewBag.SearchTerm = searchTerm;
        return View(applications);
    }
    [HttpGet]
    public ActionResult DownloadJobApplicationsExcel(int jobId)
    {
        List<ApplicationViewModel> applications = new List<ApplicationViewModel>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"SELECT A.Id, U.Name AS CandidateName, J.Title AS JobTitle, 
                        A.AppliedDate, A.Status
                        FROM Applications A
                        JOIN Users U ON A.UserId = U.Id
                        JOIN Jobs J ON A.JobId = J.Id
                        WHERE A.JobId = @JobId";

            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@JobId", jobId);
            con.Open();

            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                applications.Add(new ApplicationViewModel
                {
                    CandidateName = dr["CandidateName"].ToString(),
                    JobTitle = dr["JobTitle"].ToString(),
                    AppliedDate = Convert.ToDateTime(dr["AppliedDate"]),
                    Status = dr["Status"].ToString()
                });
            }
        }

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial; // EPPlus 5–7

        using (ExcelPackage package = new ExcelPackage())
        {
            ExcelWorksheet ws = package.Workbook.Worksheets.Add("Applications");

            // Header
            ws.Cells[1, 1].Value = "Candidate";
            ws.Cells[1, 2].Value = "Job Title";
            ws.Cells[1, 3].Value = "Applied Date";
            ws.Cells[1, 4].Value = "Status";

            using (var headerRange = ws.Cells[1, 1, 1, 4])
            {
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            }

            // Data
            for (int i = 0; i < applications.Count; i++)
            {
                var app = applications[i];
                ws.Cells[i + 2, 1].Value = app.CandidateName;
                ws.Cells[i + 2, 2].Value = app.JobTitle;
                ws.Cells[i + 2, 3].Value = app.AppliedDate.ToString("dd-MM-yyyy");
                ws.Cells[i + 2, 4].Value = app.Status;
            }

            var excelBytes = package.GetAsByteArray();
            string filename = $"Applications_Job_{jobId}.xlsx";
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
        }
    }

    public ActionResult DownloadShortlistedApplicationsExcel()
    {
        List<ApplicationViewModel> shortlistedApplications = new List<ApplicationViewModel>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"SELECT A.Id, A.AppliedDate, A.Status, A.ResumePath,
                         U.Name AS CandidateName, J.Title AS JobTitle
                         FROM Applications A
                         INNER JOIN Users U ON A.UserId = U.Id
                         INNER JOIN Jobs J ON A.JobId = J.Id
                         WHERE A.Status = 'Approved'";

            SqlCommand cmd = new SqlCommand(query, con);
            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                shortlistedApplications.Add(new ApplicationViewModel
                {
                    CandidateName = dr["CandidateName"].ToString(),
                    JobTitle = dr["JobTitle"].ToString(),
                    AppliedDate = Convert.ToDateTime(dr["AppliedDate"]),
                    Status = dr["Status"].ToString()
                });
            }
        }

        // EPPlus License Context (if not already set)
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using (ExcelPackage package = new ExcelPackage())
        {
            ExcelWorksheet ws = package.Workbook.Worksheets.Add("Shortlisted");

            // Header
            ws.Cells[1, 1].Value = "Candidate";
            ws.Cells[1, 2].Value = "Job Title";
            ws.Cells[1, 3].Value = "Applied Date";
            ws.Cells[1, 4].Value = "Status";

            using (var range = ws.Cells[1, 1, 1, 4])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            }

            // Data rows
            for (int i = 0; i < shortlistedApplications.Count; i++)
            {
                var app = shortlistedApplications[i];
                ws.Cells[i + 2, 1].Value = app.CandidateName;
                ws.Cells[i + 2, 2].Value = app.JobTitle;
                ws.Cells[i + 2, 3].Value = app.AppliedDate.ToString("dd-MM-yyyy");
                ws.Cells[i + 2, 4].Value = app.Status;
            }

            var fileBytes = package.GetAsByteArray();
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Shortlisted_Candidates.xlsx");
        }
    }

    public ActionResult DownloadPostedJobsExcel()
    {
        int employerId = Convert.ToInt32(Session["UserId"]); // ✅ FIXED HERE
        List<Job> jobs = new List<Job>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"
        SELECT 
            J.Title,
            J.Category,
            J.Location,
            J.PostedDate,
            CASE 
                WHEN J.IsApproved = 1 THEN 'Approved' 
                ELSE 'Pending' 
            END AS Status,
            (
                SELECT COUNT(*) 
                FROM Applications A 
                WHERE A.JobId = J.Id
            ) AS Applications
        FROM Jobs J
        WHERE J.PostedBy = @EmployerId";

            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@EmployerId", employerId);

            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                jobs.Add(new Job
                {
                    Title = dr["Title"].ToString(),
                    Category = dr["Category"].ToString(),
                    Location = dr["Location"].ToString(),
                    PostedDate = Convert.ToDateTime(dr["PostedDate"]),
                    Status = dr["Status"].ToString(),
                    ApplicationCount = Convert.ToInt32(dr["Applications"])
                });
            }
        }

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using (ExcelPackage package = new ExcelPackage())
        {
            var ws = package.Workbook.Worksheets.Add("Posted Jobs");

            ws.Cells[1, 1].Value = "Title";
            ws.Cells[1, 2].Value = "Category";
            ws.Cells[1, 3].Value = "Location";
            ws.Cells[1, 4].Value = "Posted Date";
            ws.Cells[1, 5].Value = "Status";
            ws.Cells[1, 6].Value = "Applications";

            using (var range = ws.Cells[1, 1, 1, 6])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            }

            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                ws.Cells[i + 2, 1].Value = job.Title;
                ws.Cells[i + 2, 2].Value = job.Category;
                ws.Cells[i + 2, 3].Value = job.Location;
                ws.Cells[i + 2, 4].Value = job.PostedDate.ToString("dd-MM-yyyy");
                ws.Cells[i + 2, 5].Value = job.Status;
                ws.Cells[i + 2, 6].Value = job.ApplicationCount;
            }

            ws.Cells.AutoFitColumns();

            var fileBytes = package.GetAsByteArray();
            return File(fileBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Posted_Jobs.xlsx");
        }
    }
    public ActionResult ViewProfile()
    {
        int userId = Convert.ToInt32(Session["UserId"]);
        User user = new User();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "SELECT * FROM Users WHERE Id = @Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Id", userId);

            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            if (dr.Read())
            {
                user.Id = (int)dr["Id"];
                user.Name = dr["Name"].ToString();
                user.Email = dr["Email"].ToString();
            }
        }

        return View(user);
    }

    [HttpPost]
    public ActionResult ViewProfile(User user)
    {
        int userId = Convert.ToInt32(Session["UserId"]);

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "UPDATE Users SET Name = @Name, Email = @Email WHERE Id = @Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Name", user.Name);
            cmd.Parameters.AddWithValue("@Email", user.Email);
            cmd.Parameters.AddWithValue("@Id", userId);

            con.Open();
            cmd.ExecuteNonQuery();
        }

        TempData["Success"] = "Profile updated successfully!";
        return RedirectToAction("ViewProfile");
    }

    public ActionResult ViewAllApplications(string searchTerm = "", string statusFilter = "")
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

    public ActionResult ManageJobs(string searchTerm = "")
    {
        if (Session["UserId"] != null)
        {
            int userId = Convert.ToInt32(Session["UserId"]);

            using (SqlConnection con = new SqlConnection(conStr))
            {
                string query = "SELECT IsBlocked FROM Users WHERE Id = @Id";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Id", userId);
                con.Open();

                bool isBlocked = Convert.ToBoolean(cmd.ExecuteScalar());
                if (isBlocked)
                {
                    return RedirectToAction("Blocked", "Account");
                }
            }
        }

        int empId = Convert.ToInt32(Session["UserId"]);
        List<Job> jobs = new List<Job>();
        int totalApplications = 0, approvedJobs = 0, pendingApps = 0, approvedApps = 0, rejectedApps = 0;

        using (SqlConnection con = new SqlConnection(conStr))
        {

            con.Open();

            string jobQuery = @"SELECT * FROM Jobs 
                                WHERE PostedBy=@PostedBy
                                AND (Title LIKE @Search OR Category LIKE @Search)";
            SqlCommand jobCmd = new SqlCommand(jobQuery, con);
            jobCmd.Parameters.AddWithValue("@PostedBy", empId);
            jobCmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");

            SqlDataReader dr = jobCmd.ExecuteReader();
            while (dr.Read())
            {
                var jobId = (int)dr["Id"];
                bool isApproved = (bool)dr["IsApproved"];
                if (isApproved) approvedJobs++;

                jobs.Add(new Job
                {
                    Id = jobId,
                    Title = dr["Title"].ToString(),
                    Description = dr["Description"].ToString(),
                    Category = dr["Category"].ToString(),
                    Location = dr["Location"].ToString(),
                    PostedDate = (DateTime)dr["PostedDate"],
                    IsApproved = isApproved,
                    ImagePath = dr["ImagePath"]?.ToString(),
                    ApplicationCount = 0
                });
            }
            dr.Close();

            foreach (var job in jobs)
            {
                string countQuery = "SELECT COUNT(*) FROM Applications WHERE JobId = @JobId";
                SqlCommand countCmd = new SqlCommand(countQuery, con);
                countCmd.Parameters.AddWithValue("@JobId", job.Id);
                job.ApplicationCount = (int)countCmd.ExecuteScalar();
            }

            string statQuery = @"SELECT Status, COUNT(*) AS Count
                                 FROM Applications A
                                 JOIN Jobs J ON A.JobId = J.Id
                                 WHERE J.PostedBy = @PostedBy
                                 GROUP BY Status";

            SqlCommand statCmd = new SqlCommand(statQuery, con);
            statCmd.Parameters.AddWithValue("@PostedBy", empId);
            SqlDataReader statDr = statCmd.ExecuteReader();

            while (statDr.Read())
            {
                string status = statDr["Status"].ToString();
                int count = (int)statDr["Count"];

                totalApplications += count;
                if (status == "Applied") pendingApps = count;
                else if (status == "Approved") approvedApps = count;
                else if (status == "Rejected") rejectedApps = count;
            }
        }

        ViewBag.TotalJobs = jobs.Count;
        ViewBag.ApprovedJobs = approvedJobs;
        ViewBag.TotalApplications = totalApplications;
        ViewBag.PendingApplications = pendingApps;
        ViewBag.ApprovedApplications = approvedApps;
        ViewBag.RejectedApplications = rejectedApps;
        ViewBag.SearchTerm = searchTerm;

        return View(jobs);
    }

}
