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
    [Table("OfferRenewBreakups")]
    public class OfferRenewBreakups
    {
        [Key]
        [JsonIgnore]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OfferRenewBreakupsID { get; set; }

        public double Spread { get; set; }

        public Ticks2.Agents Agent { get; set; }

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

        [JsonIgnore, Required, ForeignKey("OfferRenew")]
        public int OfferRenewID { get; set; }

        [JsonIgnore, Required, ForeignKey("OfferRenewID")]
        public virtual OfferRenews OfferRenew
        {
            get => _OfferRenew;
            set
            {
                _OfferRenew = value;
                if (_Customer != null) OfferRenewID = _OfferRenew.OfferRenewsID;
            }
        }
        private OfferRenews _OfferRenew;
    }
}
