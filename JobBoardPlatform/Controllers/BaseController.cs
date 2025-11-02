using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;
using JobBoardPlatform.Models;

public class BaseController : Controller
{
    string conStr = ConfigurationManager.ConnectionStrings["JobBoardDB"].ConnectionString;

    protected override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        if (Session["UserId"] != null)
        {
            int userId = Convert.ToInt32(Session["UserId"]);
            List<Notification> notifications = new List<Notification>();
            int unreadCount = 0;

            using (SqlConnection con = new SqlConnection(conStr))
            {
                con.Open();

                // Unread count
                SqlCommand cmdCount = new SqlCommand(
                    "SELECT COUNT(*) FROM Notifications WHERE UserId=@UserId AND IsRead=0", con);
                cmdCount.Parameters.AddWithValue("@UserId", userId);
                unreadCount = (int)cmdCount.ExecuteScalar();

                // Last 5 notifications
                SqlCommand cmd = new SqlCommand(
                    "SELECT TOP 5 * FROM Notifications WHERE UserId=@UserId ORDER BY CreatedAt DESC", con);
                cmd.Parameters.AddWithValue("@UserId", userId);

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

            ViewBag.UnreadNotificationCount = unreadCount;
            ViewBag.Notifications = notifications;
        }

        base.OnActionExecuting(filterContext);
    }
}
