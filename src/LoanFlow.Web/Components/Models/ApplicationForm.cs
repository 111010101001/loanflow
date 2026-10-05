using System.ComponentModel.DataAnnotations;

namespace LoanFlow.Web.Components.Models
{
    public class ApplicationForm
    {
        [Required]
        public string ApplicantName { get; set; }
        public string Email { get; set; }
        [Range(1, 10, ErrorMessage = "Amount must be between 1 and 10.")]
        public decimal Amount { get; set; }
        [Range(1, 10, ErrorMessage = "TermMonths must be between 1 and 10.")]
        public int TermMonths { get; set; }
        public decimal MonthlyIncome { get; set; }
        public decimal MonthlyExpenses { get; set; }
    }

}
