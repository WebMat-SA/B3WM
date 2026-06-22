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
    [Table("Analysis")]
    public class Analysis
    {
        public Analysis()
        {
            LastModification = DateTime.Now;
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AnalysisID { get; set; }

        [Required, ForeignKey("Author")]
        public int AuthorID { get; set; }
        
        [ForeignKey("AuthorID")]
        public virtual Users Author
        {
            get => _Author;
            set
            {
                _Author = value;
                if (_Author != null) AuthorID = _Author.UsersID;
            }
        }
        private Users _Author;

        [Required]
        public string Title { get; set; }

        [Required]
        public virtual string Reference { get; set; }

        [Required]
        public DateTime LastModification { get; set; }
    }

    public class AnalysisPrivate : Analysis
    {
        public AnalysisPrivate(Analysis a) : base()
        {
            this.AnalysisID = a.AnalysisID;
            this.Author = a.Author;
            this.AuthorID = a.AuthorID;
            this.LastModification = a.LastModification;
            this.Reference = a.Reference;
            this.Title = a.Title;
        }

        public AnalysisPrivate() { }

        [JsonIgnore]
        public override string Reference { get => base.Reference; set => base.Reference = value; }
    }
}
