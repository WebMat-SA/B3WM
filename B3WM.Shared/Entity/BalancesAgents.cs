using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    public class BalancesAgents
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BalancesAgentsID { get; set; }

        [Required, ForeignKey("Balance")]
        public int BalanceID { get; set; }

        [Required, ForeignKey("BalanceID")]
        public virtual Balances Balance
        {
            get => _Balance;
            set
            {
                _Balance = value;
                if (_Balance != null) BalanceID = _Balance.BalancesID;
            }
        }
        private Balances _Balance;

        [Required]
        public Ticks2.Agents AgentID { get; set; }

        [Required]
        public int Volume { get; set; }
    }
}
