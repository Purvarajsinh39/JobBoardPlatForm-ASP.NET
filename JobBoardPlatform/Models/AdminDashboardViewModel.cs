using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JobBoardPlatform.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalJobs { get; set; }
        public int PendingJobsCount { get; set; }
        public int ApprovedApplications { get; set; }
        public int RejectedApplications { get; set; }
        public int TotalApplications { get; set; }
        public int TotalEmployers { get; set; }

        public List<Job> PendingJobs { get; set; } // Make sure this exists


    }
}