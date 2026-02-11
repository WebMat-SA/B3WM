using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace B3WM.Shared.Entity
{
    [Table("Links")]
    public class Links
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LinksID { get; set; }

        [Required]
        public string Token { get; set; }

        [Required]
        public bool IsActive { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [JsonIgnore]
        [Required, ForeignKey("User")]
        public int UserID { get; set; }
        [JsonIgnore]
        [ForeignKey("UserID")]
        public virtual Users User
        {
            get => _User;
            set
            {
                _User = value;
                if (_User != null) UserID = _User.UsersID;
            }
        }
        private Users _User;

        private Links() {
            Date = DateTime.Now;
            IsActive = true;
        }

        public static Links LinkFactory(Users user , DataContext dbContext) 
        {
            Links newLink = new Links();

            newLink.UserID = user.UsersID;

            string newToken ;

            do
            {
                var rand = new Random().Next();

                //array de string password em bytes
                byte[] data = Encoding.ASCII.GetBytes(rand.ToString());

                //popoula o encrypted com uma string ja criptografada
                newLink.Token = BitConverter.ToString(new SHA512Managed().ComputeHash(data)).Replace("-", "").ToUpper();

            } while (dbContext.Links.FirstOrDefault(q => q.Token == newLink.Token) != null);

            dbContext.Links.Add(newLink);

            return newLink;
        }
    }
}
