using JobBoardPlatform.Models;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;

public class AccountController : Controller
{
    string conStr = ConfigurationManager.ConnectionStrings["JobBoardDB"].ConnectionString;

    public ActionResult Login() => View();

    [HttpPost]
    public ActionResult Login(string email, string password)
    {
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "SELECT * FROM Users WHERE Email=@Email AND PasswordHash=@Password";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@Password", password);

            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();
            if (dr.Read())
            {
                // ✅ Check if the user is blocked
                bool isBlocked = Convert.ToBoolean(dr["IsBlocked"]);
                if (isBlocked)
                {
                    ViewBag.Error = "Your account has been blocked by the admin.";
                    return View(); // Show error on login page
                }

                // ✅ Normal login session setup
                Session["UserId"] = dr["Id"].ToString();
                Session["UserName"] = dr["Name"].ToString();
                Session["UserRole"] = dr["Role"].ToString();

                string role = dr["Role"].ToString();
                return RedirectToAction("Dashboard", role);
            }
        }

        ViewBag.Error = "Invalid credentials!";
        return View();
    }



    public ActionResult Register() => View();

    [HttpPost]
    public ActionResult Register(User user)
    {
        using (SqlConnection con = new SqlConnection(conStr))
        {
            string query = "INSERT INTO Users (Name, Email, PasswordHash, Role) VALUES (@Name, @Email, @PasswordHash, @Role)";
            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Name", user.Name);
            cmd.Parameters.AddWithValue("@Email", user.Email);
            cmd.Parameters.AddWithValue("@PasswordHash", user.Password);  // Still passing the plain password
            cmd.Parameters.AddWithValue("@Role", user.Role);

            con.Open();
            cmd.ExecuteNonQuery();
        }
        return RedirectToAction("Login");
    }

    public ActionResult Logout()
    {
        Session.Clear();
        return RedirectToAction("Login");
    }
}
