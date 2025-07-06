using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JobBoardPlatform.Models
{
    public class Application
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public int UserId { get; set; }
        public string ResumePath { get; set; }
        public DateTime AppliedDate { get; set; }
        public string Status { get; set; }
        public string ResumeName { get; set; }

        public string SeekerName { get; set; }
        public string JobTitle { get; set; }
        
        public string CandidateName { get; set; }

        public string EmployerName { get; set; }
    }
}