using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JobBoardPlatform.Models
{
    public class JobQuestion
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public string QuestionText { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        public string CorrectOption { get; set; }  // Changed to string
        public string SelectedAnswer { get; set; }  // New: For seeker selections
    }
}