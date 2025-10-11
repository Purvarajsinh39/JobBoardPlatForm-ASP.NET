using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace JobBoardPlatform.Models
{
    public class QuizStatsViewModel
    {
        public int TotalAttempts { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
    }
}