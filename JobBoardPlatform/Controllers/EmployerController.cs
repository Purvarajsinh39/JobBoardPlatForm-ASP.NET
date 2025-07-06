// ===============================
// EmployerController.cs
// ===============================

using JobBoardPlatform.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;

public class EmployerController : Controller
{
    string conStr = ConfigurationManager.ConnectionStrings["JobBoardDB"].ConnectionString;

    public ActionResult Dashboard()
    {
        int empId = Convert.ToInt32(Session["UserId"]);
        List<Job> jobs = new List<Job>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "SELECT * FROM Jobs WHERE PostedBy=@PostedBy";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@PostedBy", empId);
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
                    IsApproved = (bool)dr["IsApproved"]
                });
            }
        }
        return View(jobs);
    }

    public ActionResult CreateJob() => View();

    [HttpPost]
    public ActionResult CreateJob(Job job)
    {
        job.PostedBy = Convert.ToInt32(Session["UserId"]);
        job.PostedDate = DateTime.Now;
        job.IsApproved = false;

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"INSERT INTO Jobs (Title, Description, Category, Location, PostedBy, PostedDate, IsApproved)
                             VALUES (@Title, @Description, @Category, @Location, @PostedBy, @PostedDate, @IsApproved)";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Title", job.Title);
            cmd.Parameters.AddWithValue("@Description", job.Description);
            cmd.Parameters.AddWithValue("@Category", job.Category);
            cmd.Parameters.AddWithValue("@Location", job.Location);
            cmd.Parameters.AddWithValue("@PostedBy", job.PostedBy);
            cmd.Parameters.AddWithValue("@PostedDate", job.PostedDate);
            cmd.Parameters.AddWithValue("@IsApproved", job.IsApproved);

            con.Open();
            cmd.ExecuteNonQuery();
        }
        return RedirectToAction("Dashboard");
    }

    public ActionResult ViewApplications(int id)
    {
        List<Application> applications = new List<Application>();

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = @"SELECT A.*, U.Name AS CandidateName, J.Title AS JobTitle
                         FROM Applications A
                         JOIN Users U ON A.UserId = U.Id
                         JOIN Jobs J ON A.JobId = J.Id
                         WHERE A.JobId = @JobId";

            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@JobId", id);

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


    [HttpPost]
 
    public ActionResult UpdateApplicationStatus(int applicationId, string status)
    {
        string conStr = ConfigurationManager.ConnectionStrings["JobBoardDB"].ConnectionString;

        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "UPDATE Applications SET Status=@Status WHERE Id=@Id";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@Id", applicationId);
            con.Open();
            cmd.ExecuteNonQuery();
        }

        return RedirectToAction("Dashboard"); // or redirect to ViewApplications with jobId if you prefer
    }

}
