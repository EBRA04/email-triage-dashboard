using EmailTriage.Api.Models;

namespace EmailTriage.Api.Data;

public static class DbSeeder
{
    private static readonly (string Name, string Slug)[] Departments =
    {
        ("Human Resources", "hr"),
        ("IT Support", "it"),
        ("Marketing", "marketing"),
        ("Sales", "sales"),
    };

    public static void Seed(AppDbContext db)
    {
        db.Database.EnsureCreated();

        foreach (var (name, slug) in Departments)
        {
            if (!db.Departments.Any(d => d.Slug == slug))
            {
                db.Departments.Add(new Department { Name = name, Slug = slug });
            }
        }
        db.SaveChanges();

        var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("password123");

        if (!db.Users.Any(u => u.Email == "admin@company.test"))
        {
            db.Users.Add(new User
            {
                Email = "admin@company.test",
                PasswordHash = defaultPasswordHash,
                Name = "Super Admin",
                Role = Role.SuperAdmin
            });
        }

        foreach (var (name, slug) in Departments)
        {
            var email = $"{slug}@company.test";
            if (!db.Users.Any(u => u.Email == email))
            {
                var dept = db.Departments.First(d => d.Slug == slug);
                db.Users.Add(new User
                {
                    Email = email,
                    PasswordHash = defaultPasswordHash,
                    Name = $"{name} Agent",
                    Role = Role.DeptAdmin,
                    DepartmentId = dept.Id
                });
            }
        }

        db.SaveChanges();
    }
}
