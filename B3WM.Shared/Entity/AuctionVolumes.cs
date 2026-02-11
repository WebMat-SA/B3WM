using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    [Table("AuctionVolumes")]
    public class AuctionVolumes
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AuctionVolumesID { get; set; }

        [Required, ForeignKey("Customer")]
        public int CustomerID { get; set; }

        [ForeignKey("CustomerID")]
        public virtual Customers Customer { get; set; }

        public DateTime Date { get; set; }

        public AuctionType TypeAuction { get; set; }

        public long Value { get; set; }

        public enum AuctionType
        {
            Opening,
            Closing,
        }
    }
}
