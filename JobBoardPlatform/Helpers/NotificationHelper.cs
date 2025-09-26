using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;

namespace JobBoardPlatform.Helpers
{
    public class NotificationHelper
    {
        private string conStr = ConfigurationManager.ConnectionStrings["JobBoardDB"].ConnectionString;

        public void AddNotification(int userId, string message)
        {
            using (SqlConnection con = new SqlConnection(conStr))
            {
                string query = "INSERT INTO Notifications (UserId, Message, IsRead, CreatedAt) VALUES (@UserId, @Message, 0, GETDATE())";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Message", message);

                con.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
