using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoiceOverLAN
{
    public enum Status { Inactive = 0, Active = 1, Brb = 2 };

    public class Contact
    {
        public string Nickname;
        public string IpAddress;
        [JsonIgnore]
        public Status Status;

        public Contact()
        {
            this.Nickname = "Placeholder";
            this.IpAddress = "127.0.0.1";
            this.Status = Status.Inactive;
        }

        public Contact(string nickname, string ipAddress)
        {
            this.Nickname = nickname;
            this.IpAddress = ipAddress;
            this.Status = Status.Inactive;
        }
    }
}
