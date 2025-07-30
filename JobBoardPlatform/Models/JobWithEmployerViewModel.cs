using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JobBoardPlatform.Models
{
    public class JobWithEmployerViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Location { get; set; }
        public DateTime PostedDate { get; set; }
        public bool IsApproved { get; set; }
        public string EmployerName { get; set; }
    }
}