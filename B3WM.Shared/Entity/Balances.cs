using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    [Table("Balances")]
    public class Balances
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BalancesID { get; set; }

        [Required]
        public DateTime Date { get; set; }

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

        public ICollection<BalancesAgents> Agents { get; set; } = new Collection<BalancesAgents>();
    }
}
