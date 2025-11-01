using Microsoft.AspNetCore.Identity;
using System.Xml.Linq;

namespace ServerSignaling_Meeting.Data
{
    public class AppRole : IdentityRole<Guid>
    {
        public AppRole()
        {
            Id = Guid.NewGuid();
        }

        public AppRole(string roleName) : this()
        {
            Name = roleName;
        }
    }
}
