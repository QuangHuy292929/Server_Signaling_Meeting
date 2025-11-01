using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServerSignaling_Meeting.Models;
using System.Reflection.Emit;

namespace ServerSignaling_Meeting.Data
{
    public class ApplicationDbContext: IdentityDbContext<AppUser, AppRole, Guid>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> dbContextOptions) : base(dbContextOptions)
        {
        }
        public DbSet<AppRole> AppRoles { get; set; }
        public DbSet<GroupChat> GroupChats { get; set; }
        public DbSet<JoinGroup> JoinGroups { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<RoomMeeting> RoomMeetings { get; set; }
        public DbSet<JoinMeeting> JoinMeetings { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<JoinGroup>()
                .HasOne(j => j.User)
                .WithMany(u => u.JoinGroups)
                .HasForeignKey(j => j.UserId);

            builder.Entity<JoinGroup>()
                .HasOne(j => j.GroupChat)
                .WithMany(g => g.JoinGroups)
                .HasForeignKey(j => j.GroupId);

            builder.Entity<ChatMessage>()
                .HasOne(m => m.User)
                .WithMany(u => u.ChatMessages)
                .HasForeignKey(m => m.UserId);

            builder.Entity<ChatMessage>()
                .HasOne(m => m.GroupChat)
                .WithMany(g => g.ChatMessages)
                .HasForeignKey(m => m.GroupId);

            builder.Entity<JoinMeeting>()
                .HasOne(j => j.User)
                .WithMany(u => u.JoinMeetings)
                .HasForeignKey(j => j.UserId);

            builder.Entity<JoinMeeting>()
                .HasOne(j => j.RoomMeeting)
                .WithMany(r => r.JoinMeetings)
                .HasForeignKey(j => j.RoomId);

            List<AppRole> roles = new List<AppRole>
            {
                new AppRole
                {
                    Id = Guid.NewGuid(),
                    Name = "Admin",
                    NormalizedName = "ADMIN"
                },
                new AppRole
                {
                    Id = Guid.NewGuid(),
                    Name = "User",
                    NormalizedName = "USER"
                }
            };

            builder.Entity<AppRole>().HasData(roles);
        }
    }
}
