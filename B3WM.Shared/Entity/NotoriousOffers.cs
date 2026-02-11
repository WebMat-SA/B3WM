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
    [Table("NotoriousOffers")]
    public class NotoriousOffers
    {
        [Key]
        [JsonIgnore]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NotoriousOffersID { get; set; }

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

        [JsonIgnore, Required]
        public DateTime Date { get; set; }

        [Required]
        public Ticks2.Agents Agent { get; set; }

        [Required]
        public Ticks2.ActionType TypeAction { get; set; }

        [Required]
        public int Volume { get; set; }

        [JsonIgnore, Required]
        public double Value { get; set; }



        public static double AverageNotorious(List<Ticks2> ticks)
        {
            return (ticks.Sum(q => q.Volume) / ticks.Count) * 2.4; //140% a mais da media
        }

        public static double AverageNotorious(List<BookItem> ticks)
        {
            return (ticks.Sum(q => q.Volume) / ticks.Count) * 2.4; //140% a mais da media
        }
    }

    [Table("NotoriousOffers2")]
    public class NotoriousOffers2  
    {

        public NotoriousOffers2() { }
        public NotoriousOffers2(NotoriousOffers no)
        {
            Customer = no.Customer;
            Agent = no.Agent;
            Date = no.Date;
            TypeAction = no.TypeAction;
            Volume = no.Volume;
            Value = no.Value;
        }

        [Key]
        [JsonIgnore]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NotoriousOffersID { get; set; }

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

        [JsonIgnore, Required]
        public DateTime Date { get; set; }

        [Required]
        public Ticks2.Agents Agent { get; set; }

        [Required]
        public Ticks2.ActionType TypeAction { get; set; }

        [Required]
        public int Volume { get; set; }

        [JsonIgnore, Required]
        public double Value { get; set; }



        public static double AverageNotorious(List<Ticks2> ticks)
        {
            return (ticks.Sum(q => q.Volume) / ticks.Count) * 2.4; //140% a mais da media
        }

        public static double AverageNotorious(List<BookItem> ticks)
        {
            return (ticks.Sum(q => q.Volume) / ticks.Count) * 2.4; //140% a mais da media
        }
    }
}
