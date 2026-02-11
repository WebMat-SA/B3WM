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
    [Table("VWapDistortions")]
    public class VWapDistortions
    {

        public VWapDistortions() { }

        public VWapDistortions(IEnumerable<Bars> bars, double lastVwap)
        {
            var lastBar = bars.OrderByDescending(q => q.Date).FirstOrDefault();

            if (lastBar != null)
            {
                this.Date = lastBar.Date;
                this.MaxValue = lastBar.High - lastVwap;
                this.MaxValuePercent = (MaxValue / lastBar.High) * 100.0;
                this.MinValue = lastBar.Low - lastVwap;
                this.MinValuePercent = (MinValue / lastBar.Low) * 100.0;
                this.Customer = lastBar.Customer;
                this.VWapValue = lastVwap;
            }
        }

        [JsonIgnore]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int VWapDistortionsID { get; set; }

        [Required, ForeignKey("Customer")]
        public int CustomerID { get; set; }

        [Required, ForeignKey("CustomerID")]
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
        public DateTime Date { get; set; }

        [Required]
        public double MaxValue { get; set; }

        [Required]
        public double MaxValuePercent { get ; set; }

        [Required]
        public double MinValue { get; set; }

        [Required]
        public double MinValuePercent { get; set; }

        [Required]
        public double VWapValue { get; set; }

    }
}
