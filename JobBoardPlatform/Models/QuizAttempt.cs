using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JobBoardPlatform.Models
{
    public class QuizAttempt
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public int UserId { get; set; }
        public int Score { get; set; }
        public bool Passed { get; set; }
        public DateTime AttemptDate { get; set; }
        public string SeekerName { get; set; }  // For display
        public string JobTitle { get; set; }    // For display
    }
}