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
    
    public class SequentialAgent
    {
        [Required]
        public Ticks2.Agents Agent { get; set; }

        [Required]
        public Ticks2.ActionType TypeAction { get; set; }

        [JsonIgnore, Required]
        public double Value { get; set; }

        [Required]
        public DateTime Date { get; set; }

        public override string ToString()
        {
            return $"{Agent} : {(TypeAction == Ticks2.ActionType.Buy ? "Compra" : "Venda")} : {Date.ToString("HH:mm:ss")}";
        }
    }

    [Table("SequentialActions")]
    public class SequentialActions : SequentialAgent
    {
        [Key]
        [JsonIgnore]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SequentialActionsID { get; set; }

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
