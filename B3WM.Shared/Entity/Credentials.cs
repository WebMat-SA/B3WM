using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    [Table("Credentials")]
    public class Credentials
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CredentialsID { get; set; }

        [Required]
        public string Code { get; set; }

        [Required]
        public string DisplayName { get; set; }

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

        [Required]
        public bool IsActive { get; set; }
        
        public DateTime? ExpirationDate { get; set; }

        [JsonIgnore]
        [Required, ForeignKey("Plan")]
        public int PlanID { get; set; }
        [ForeignKey("PlanID")]
        public virtual Plans Plan
        {
            get => _Plan;
            set
            {
                _Plan = value;
                if (_Plan != null) PlanID = _Plan.PlansID;
            }
        }
        private Plans _Plan;

        public ICollection<CredentialsCustomers> WatchListCustomers { get; set; } = new Collection<CredentialsCustomers>();

        public static async Task<Credentials> CredentialFactory(Users user, DataContext dbContext, string name)
        {
            var plan = await dbContext.Plans.FirstOrDefaultAsync(q=>q.PlansID == user.Plan.PlansID);

            Credentials newCred = new Credentials()
            {
                User = user,
                Plan = plan,
                DisplayName = name,
                ExpirationDate = null,
                IsActive = true
            };

            string newToken;

            do
            {
                var rand = new Random().Next();

                //array de string password em bytes
                byte[] data = Encoding.ASCII.GetBytes(rand.ToString());

                //popoula o encrypted com uma string ja criptografada
                newCred.Code = BitConverter.ToString(new SHA512Managed().ComputeHash(data)).Replace("-", "").ToUpper();

            } while (dbContext.Credentials.FirstOrDefault(q => q.Code == newCred.Code) != null);

            //dbContext.Links.Add(newLink);

            return newCred;
        }

    }
}
