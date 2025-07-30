using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JobBoardPlatform.Models
{
    public class Job
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Location { get; set; }
        public DateTime PostedDate { get; set; }
        public int PostedBy { get; set; }
        public string PostedByName { get; set; }
        public bool IsApproved { get; set; }
        public int ApplicationCount { get; set; }
        public bool AlreadyApplied { get; set; }
        public string ImagePath { get; set; }

        public string Status { get; set; }
        public int Applications { get; set; }
        public string EmployerName { get; set; }



    }
}