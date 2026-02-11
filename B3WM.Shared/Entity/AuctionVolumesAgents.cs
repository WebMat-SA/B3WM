using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    [Table("AuctionVolumesAgents")]
    public class AuctionVolumesAgents
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AuctionVolumesAgentsID { get; set; }

        [Required]
        public Ticks2.Agents AgentID { get; set; }

        [Required]
        public int Volume { get; set; }

        [Required, ForeignKey("AuctionVolume")]
        public int AuctionVolumeID { get; set; }

        [Required, ForeignKey("AuctionVolumeID")]
        public virtual AuctionVolumes AuctionVolume
        {
            get => _AuctionVolume;
            set
            {
                _AuctionVolume = value;
                if (_AuctionVolume != null) AuctionVolumeID = _AuctionVolume.AuctionVolumesID;
            }
        }
        private AuctionVolumes _AuctionVolume;

    }
}
