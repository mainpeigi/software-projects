using System.ComponentModel.DataAnnotations;

namespace DAtemulte_2.Models
{
    public class UserPreferences
    {
        [Key]
        public Guid Id { get; set; } =  Guid.NewGuid();
        public Guid UserId { get; set; } 
        public string Density { get; set; } = "Comfortable";
        public string DefaultChartType { get; set; } = "Line";
        public bool HighContrast { get; set; } = false;
        public double FontSizeMultiplier { get; set; } = 1.0;
    }
}