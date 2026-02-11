using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    [Table("Setups")]
    public class Setups
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SetupsID { get; set; }

        [Required, ForeignKey("User")]
        public int UserID { get; set; }

        [ForeignKey("UserID")]
        public virtual Users User { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public DateTime DateCreate { get; set; }

        public double? LastPrice { get; set; }

        public int? TicksCount { get; set; }

        public bool? BuyerAnomaly { get; set; }
        public bool? SellerAnomaly { get; set; }

        public bool? RobotBuyer { get; set; }

        public bool? RobotSeller { get; set; }

        public int? ItemsToShow { get; set; }

        public double? Spread { get; set; }
        public double? Vol1Min { get; set; }

        public bool? Muted { get; set; }

        public bool? AutoScroll { get; set; }

        public string OrderBy { get; set; }

        public bool? Resumed { get; set; }

        public bool? IsOnlyFixedAudio { get; set; }
        public int? OfferRenewMinVolume { get; set; }

        public B3WM.Shared.Extensions.OrderType? OrderByType { get; set; }

        [NotMapped]
        public string _SequentialNotoriousAgentsJson;
        public string SequentialNotoriousAgentsJson 
        {
            get => _SequentialNotoriousAgentsJson;
            set 
            {
                _SequentialNotoriousAgentsJson = value;
                
            } 
        }

        [NotMapped]
        public Ticks2.Agents[] SequentialNotoriousAgents 
        {
            get => !string.IsNullOrEmpty(_SequentialNotoriousAgentsJson) ? System.Text.Json.JsonSerializer.Deserialize<Ticks2.Agents[]>(_SequentialNotoriousAgentsJson) : null;
            set
            {
                _SequentialNotoriousAgentsJson = System.Text.Json.JsonSerializer.Serialize(value);
            }
        }

        public int? SequentialNotoriousMinVolume { get; set; }
        //public ICollection<SetupsCustomers> Customers { get; set; } = new Collection<SetupsCustomers>();


    }
}
