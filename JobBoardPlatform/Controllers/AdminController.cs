using JobBoardPlatform.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;

public class AdminController : Controller
{
    string conStr = ConfigurationManager.ConnectionStrings["JobBoardDB"].ConnectionString;

    public ActionResult Dashboard()
    {
        if (Session["UserRole"]?.ToString() != "Admin")
            return RedirectToAction("Login", "Account");

        List<Job> jobs = new List<Job>();
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "SELECT J.*, U.Name as EmployerName FROM Jobs J JOIN Users U ON J.PostedBy = U.Id WHERE IsApproved = 0";
            SqlCommand cmd = new SqlCommand(query, con);
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

    public ActionResult Approve(int id)
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

    public ActionResult Reject(int id)
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
                             WHERE A.Status = 'Applied'"; // ✅ Filter only new apps

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

        return View(apps); // Applications.cshtml
    }

    public ActionResult ViewApplications()
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
            JOIN Users E ON J.PostedBy = E.Id";

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
                    JobTitle = dr["JobTitle"].ToString(),
                    EmployerName = dr["EmployerName"].ToString(),
                    CandidateName = dr["CandidateName"].ToString()
                });
            }
        }

        return View(apps); // ViewApplications.cshtml
    }
}
