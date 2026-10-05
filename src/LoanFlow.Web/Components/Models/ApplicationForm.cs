using System.ComponentModel.DataAnnotations;

namespace LoanFlow.Web.Components.Models
{
    public class ApplicationForm
    {
        [Required]
        public string ApplicantName { get; set; } = "";
        [Required]
        public string Email { get; set; } = "";
        [Range(1000, 500000, ErrorMessage = "Amount must be between 1000 and 500 000.")]
        public decimal Amount { get; set; }
        [Range(6, 120, ErrorMessage = "TermMonths must be between 6 and 120.")]
        public int TermMonths { get; set; }
        public decimal MonthlyIncome { get; set; }
        public decimal MonthlyExpenses { get; set; }
    }

}
