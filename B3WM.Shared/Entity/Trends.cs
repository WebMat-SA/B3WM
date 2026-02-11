using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    [Table("Trends")]
    public class Trends
    {
        [Key]
        [JsonIgnore]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TrendsID { get; set; }

        [JsonIgnore, Required, ForeignKey("Customer")]
        public int CustomerID { get; set; }

        [JsonIgnore, Required, ForeignKey("CustomerID")]
        public virtual Customers Customer
        {
            get => _Customer;
            set
            {
                _Customer = value;
                if (_Customer != null) CustomerID = _Customer.CustomersID;
            }
        }
        private Customers _Customer;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public double Angle { get; set; }

        [Required]
        public TypeTrend Type { get; set; }

        [Required]
        public Bars.TypeTimes TypeTime { get; set; }

        [NotMapped]
        public TimeSpan Interval => EndDate - StartDate;

        [NotMapped]
        public double Last { get; set; }

        public enum TypeTrend
        {
            Up,
            Down
        }

        public override string ToString()
        {
            return $"{Customer.Symbol} => Start: {StartDate.ToString("dd/MM/yy HH:mm:ss")} , How Long: {Interval.TotalDays} days | Type: {(Type == TypeTrend.Down ? "Venda" : "Compra")} | Last : {Last.ToString("0.00")}  | Angle: {Angle.ToString("0.000")}º";
        }

    }
}
