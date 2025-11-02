using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JobBoardPlatform.Models
{
    public class CreateJobViewModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Location { get; set; }
        public HttpPostedFileBase Image { get; set; }
        public List<JobQuestion> Questions { get; set; } = new List<JobQuestion>();
    }
}