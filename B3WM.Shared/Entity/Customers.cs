using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    [Table("Customers")]
    public class Customers
    {
        public Customers() { }

        public Customers(string _Symbol, string _Name, bool _IsEnabled)
        {
            Symbol = _Symbol;
            Name = _Name;
            IsEnabled = _IsEnabled;
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CustomersID { get; set; }

        [Required, MinLength(2), MaxLength(100), Display(Name = "Name")]
        public string Name { get; set; }

        [Required, MinLength(2), MaxLength(50), Display(Name = "Symbol")]
        public string Symbol { get; set; }

        [Required]
        public bool OnlyCandles { get; set; }

        //[Column(TypeName = "bit")]
        [JsonIgnore]
        [Required, Display(Name = "Is Enabled?")]
        public bool IsEnabled { get; set; }

        [ForeignKey("Segment")]
        public int? SegmentID { get; set; }

        [JsonIgnore]
        [ForeignKey("SegmentID")]
        public virtual Segments Segment
        {
            get => _Segment;
            set
            {
                _Segment = value;
                if (_Segment != null) SegmentID = _Segment.SegmentsID;
            }
        }
        private Segments _Segment;

    }
}
