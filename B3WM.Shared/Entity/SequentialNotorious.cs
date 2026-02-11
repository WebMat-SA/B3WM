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
    [Table("SequentialNotorious")]
    public class SequentialNotorious
    {
        [Key]
        [JsonIgnore]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SequentialNotoriousID { get; set; }

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

        [JsonIgnore, Required, ForeignKey("NotoriousOffer")]
        public int NotoriousOfferID { get; set; }

        [Required, ForeignKey("NotoriousOfferID")]
        public virtual NotoriousOffers2 NotoriousOffer
        {
            get => _NotoriousOffer;
            set
            {
                _NotoriousOffer = value;
                if (_NotoriousOffer != null) NotoriousOfferID = _NotoriousOffer.NotoriousOffersID;
            }
        }
        private NotoriousOffers2 _NotoriousOffer;


        [JsonIgnore, Required, ForeignKey("SequentialAction")]
        public int SequentialActionID { get; set; }

        [Required, ForeignKey("SequentialActionID")]
        public virtual SequentialActions SequentialAction
        {
            get => _SequentialAction;
            set
            {
                _SequentialAction = value;
                if (_SequentialAction != null) SequentialActionID = _SequentialAction.SequentialActionsID;
            }
        }
        private SequentialActions _SequentialAction;

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public Ticks2.Agents Agent { get; set; }

        [Required]
        public double Spread { get; set; }

        public override string ToString()
        {
            return $"SequentialNotorious => Customer: {Customer.Symbol} || Agent: {Agent} || Sequential Action: {SequentialAction.Date.ToString("HH:mm:ss")} || Notorious Offer: {NotoriousOffer.Date.ToString("HH:mm:ss")}";
        }

        [NotMapped]
        public int SequentialActionsStarted { get; set; }
    }
}
