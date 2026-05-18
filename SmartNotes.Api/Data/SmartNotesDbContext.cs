using Microsoft.EntityFrameworkCore;
using SmartNotes.Api.Models;

namespace SmartNotes.Api.Data
{
    public class SmartNotesDbContext : DbContext
    {
        public SmartNotesDbContext(DbContextOptions<SmartNotesDbContext> options)
            : base(options)
        {
        }

        public DbSet<Note> Notes { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<TranscriptionRecord> Transcriptions { get; set; }
        public DbSet<RaspberryDevice> RaspberryDevices { get; set; }
        public DbSet<Classroom> Classrooms { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<UserSubscription> UserSubscriptions { get; set; }
    }
}