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
        ViewBag.Msg = TempData["Msg"];
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

        // New: Check if job has quiz
        bool hasQuiz = false;
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "SELECT COUNT(*) FROM JobQuestions WHERE JobId = @JobId";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@JobId", jobId);
            con.Open();
            hasQuiz = (int)cmd.ExecuteScalar() > 0;
        }

        if (hasQuiz)
        {
            // Redirect to TakeQuiz, carry resumePath
            var quizModel = new QuizViewModel { JobId = jobId, ResumePath = resumePath };
            return RedirectToAction("TakeQuiz", new { jobId = jobId, resumePath = resumePath });
        }
        else
        {
            // Old logic: Insert application directly
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

            // Old: Get EmployerId + JobTitle and notify
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

            var helper = new JobBoardPlatform.Helpers.NotificationHelper();
            helper.AddNotification(employerId, $"A candidate applied for your job: {jobTitle}");

            TempData["Msg"] = "Applied with resume!";
            return RedirectToAction("Dashboard");
        }
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
        ViewBag.Msg = TempData["Msg"];
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


    // GET: TakeQuiz (new)
    // GET: TakeQuiz
    public ActionResult TakeQuiz(int jobId, string resumePath)
    {
        var model = new QuizViewModel { JobId = jobId, ResumePath = resumePath };

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "SELECT Id, QuestionText, OptionA, OptionB, OptionC, OptionD FROM JobQuestions WHERE JobId = @JobId";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@JobId", jobId);
            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                model.Questions.Add(new JobQuestion
                {
                    Id = (int)dr["Id"],
                    QuestionText = dr["QuestionText"].ToString(),
                    OptionA = dr["OptionA"].ToString(),
                    OptionB = dr["OptionB"].ToString(),
                    OptionC = dr["OptionC"].ToString(),
                    OptionD = dr["OptionD"].ToString()
                });
            }
        }

        return View(model);
    }

    // POST: TakeQuiz (new, process answers)
    [HttpPost]
    // POST: TakeQuiz
   
    public ActionResult TakeQuiz(QuizViewModel model)
    {
        try
        {
            // Load correct answers from DB
            var corrects = new Dictionary<int, string>();
            using (SqlConnection con = new SqlConnection(conStr))
            {
                string query = "SELECT Id, CorrectOption FROM JobQuestions WHERE JobId = @JobId";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@JobId", model.JobId);
                con.Open();
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    corrects.Add((int)dr["Id"], dr["CorrectOption"].ToString());
                }
            }

            // Calculate score
            int correctCount = 0;
            foreach (var q in model.Questions)
            {
                if (!string.IsNullOrEmpty(q.SelectedAnswer) && corrects.TryGetValue(q.Id, out string correctOption) && q.SelectedAnswer == correctOption)
                {
                    correctCount++;
                }
            }
            int total = model.Questions.Count;
            int score = total > 0 ? (correctCount * 100 / total) : 0;
            bool passed = score >= 80;

            // Record attempt
            using (SqlConnection con = new SqlConnection(conStr))
            {
                string query = @"INSERT INTO QuizAttempts (JobId, UserId, Score, Passed)
                             VALUES (@JobId, @UserId, @Score, @Passed)";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@JobId", model.JobId);
                cmd.Parameters.AddWithValue("@UserId", Convert.ToInt32(Session["UserId"]));
                cmd.Parameters.AddWithValue("@Score", score);
                cmd.Parameters.AddWithValue("@Passed", passed);
                con.Open();
                cmd.ExecuteNonQuery();
            }

            if (passed)
            {
                // Create application
                using (SqlConnection con = new SqlConnection(conStr))
                {
                    string query = @"INSERT INTO Applications (JobId, UserId, ResumePath, AppliedDate, Status)
                                 VALUES (@JobId, @UserId, @ResumePath, GETDATE(), 'Applied')";
                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@JobId", model.JobId);
                    cmd.Parameters.AddWithValue("@UserId", Convert.ToInt32(Session["UserId"]));
                    cmd.Parameters.AddWithValue("@ResumePath", model.ResumePath);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }

                // Notify employer
                int employerId = 0;
                string jobTitle = "";
                using (SqlConnection con = new SqlConnection(conStr))
                {
                    string query = "SELECT PostedBy, Title FROM Jobs WHERE Id=@JobId";
                    SqlCommand cmd = new SqlCommand(query, con);
                    cmd.Parameters.AddWithValue("@JobId", model.JobId);
                    con.Open();
                    SqlDataReader dr = cmd.ExecuteReader();
                    if (dr.Read())
                    {
                        employerId = (int)dr["PostedBy"];
                        jobTitle = dr["Title"].ToString();
                    }
                }

                var helper = new JobBoardPlatform.Helpers.NotificationHelper();
                helper.AddNotification(employerId, $"A candidate applied for your job: {jobTitle} after passing quiz.");

                TempData["Msg"] = $"Quiz passed with score {score}%! Application submitted.";
            }
            else
            {
                TempData["Msg"] = $"Quiz failed with score {score}%. Application not submitted. You can try again.";
            }

            return RedirectToAction("Dashboard");
        }
        catch (Exception ex)
        {
            TempData["Msg"] = $"Error during quiz submission: {ex.Message}";
            return RedirectToAction("Dashboard");
        }
    }

}
