using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using System.Linq;

namespace B3WM.Shared.Entity
{
    [Table("Ticks")]
    public class Ticks 
    {
        public static Ticks Factory(TimesAndSales tns)
        {
            var tick = tns.Ticks.LastOrDefault();
            if (tick == null)
                return null;
            else
            {
                Ticks Tick = new Ticks();
                Tick.Time = tick.Time;
                Tick.Bid = tns.LastBid;
                Tick.Ask = tns.LastAsk;
                Tick.Last = tns.Last;
                Tick.IsAuction = tns.IsInAuction;
                Tick.Customer = tns.Customer;
                Tick.CustomerID = tns.Customer.CustomersID;
                return Tick;
            }
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TicksID { get; set; }

        [Required]
        public DateTime Time { get; set; }
        [Required]
        public double Bid { get; set; }
        [Required]
        public double Ask { get; set; }
        [JsonIgnore]
        [Required, ForeignKey("Customer")]
        public int CustomerID { get; set; }
        [ForeignKey("CustomerID")]
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

        [NotMapped]
        public bool IsAuction { get; set; }

        [NotMapped]
        public double Last { get; set; }
        [NotMapped]
        public long Volume { get; set; }
        [NotMapped]
        public long VolumeReal { get; set; }
        [NotMapped]
        public int Flags { get; set; }


        public override string ToString()
        {
            return $"{Customer.Symbol} - {Last} - {Ask} - {Bid} - {Time} - {IsAuction}";
        }
    }
}
