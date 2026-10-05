using System.ComponentModel.DataAnnotations;

namespace LoanFlow.Web.Components.Models
{
    public class ApplicationForm
    {
        [Required]
        public string ApplicantName { get; set; } = "";
        [Required]
        public string Email { get; set; } = "";
        [Range(1000, 500000, ErrorMessage = "{0} must be between 1000 and 500 000.")]
        public decimal Amount { get; set; }
        [Range(6, 120, ErrorMessage = "{0} must be between 6 and 120.")]
        public int TermMonths { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "{0} can't be negative.")]
        public decimal MonthlyIncome { get; set; }
        [ Range(0, double.MaxValue, ErrorMessage = "{0} can't be negative.")]
        public decimal MonthlyExpenses { get; set; }
    }

}
