using B3WM.Shared.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace B3WM.Shared.Extensions
{
    public class UsersTokens : IUsersTokens
    {
        public Users User { get; set; }
        public string Token { get; set; }
        [JsonIgnore]
        public DateTime LastPing { get; set; }

        public UsersTokens Clone()
        {
            return (UsersTokens)this.MemberwiseClone();
        }

    }
    public interface IUsersTokens
    {
        Users User { get; set; }
        string Token { get; set; }
        [JsonIgnore]
        public DateTime LastPing { get; set; }
    }
}
