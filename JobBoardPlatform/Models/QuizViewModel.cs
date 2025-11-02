using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JobBoardPlatform.Models
{
    public class QuizViewModel
    {
        public int JobId { get; set; }
        public string ResumePath { get; set; }
        public List<JobQuestion> Questions { get; set; } = new List<JobQuestion>();
    }
}