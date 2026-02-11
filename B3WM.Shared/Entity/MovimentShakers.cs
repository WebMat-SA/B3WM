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
    [Table("MovimentShakers")]
    public class MovimentShakers
    {
        [Key]
        [JsonIgnore]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MovimentShakersID { get; set; }

        [Required]
        public Ticks2.Agents Agent { get; set; }
        [Required]
        public long Volume { get; set; }

        [Required]
        public double MaxValue { get; set; }
        [Required]
        public double MinValue { get; set; }

        [Required]
        public DateTime LastIncident { get; set; }
        [Required]
        public DateTime FirstIncident { get; set; }


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

        [JsonIgnore]
        public double Range
        {
            get => MaxValue - MinValue;
        }

        [JsonIgnore]
        public TimeSpan Duration
        {
            get => FirstIncident - LastIncident;
        }
    }
}
