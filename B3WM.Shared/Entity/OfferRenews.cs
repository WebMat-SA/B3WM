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
    [Table("OfferRenews")]
    public class OfferRenews
    {
        [Key]
        [JsonIgnore]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OfferRenewsID { get; set; }

        [Required]
        public Ticks2.Agents Agent { get; set; }

        [Required]
        public Ticks2.ActionType Type { get; set; }

        [Required]
        public double Value { get; set; }

        [Required]
        public int Volume { get; set; }

        [Required]
        public DateTime Date { get; set; }

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
    }
}
