using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    [Table("Segments")]
    public class Segments
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SegmentsID { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        [ForeignKey("SubSector")]
        public int SubSectorID { get; set; }

        [ForeignKey("SubSectorID")]
        public virtual SubSectors SubSector
        {
            get => _SubSector;
            set
            {
                _SubSector = value;
                if (_SubSector != null) SubSectorID = _SubSector.SubSectorsID;
            }
        }
        private SubSectors _SubSector;
    }
}
