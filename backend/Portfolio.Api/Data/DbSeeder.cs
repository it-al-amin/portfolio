using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Entities;
using Portfolio.Api.Services;

namespace Portfolio.Api.Data;

/// <summary>
/// One-time initial content taken from Md Al-Amin's CV ("Al_Amin_Full_Stack_Software_Engineer.pdf",
/// with the earlier "Al-Amin(Nearly3Years).pdf") and cover letter. Runs only when the database has no profile,
/// so it never overwrites edits made in the admin dashboard. Unknown facts are left empty, not invented.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(PortfolioDbContext db, FileStorageService storage, IWebHostEnvironment env, ILogger logger)
    {
        if (await db.Profiles.AnyAsync()) return;

        db.Profiles.Add(new Profile
        {
            FullName = "Md Al-Amin",
            Headline = "Full-Stack Software Engineer",
            Tagline = "C# · ASP.NET Core · React · Angular · SQL — building secure, scalable web applications.",
            Summary = "Software Engineer with nearly 3 years of professional experience, a strong foundation in algorithms, data structures and full-stack development, and hands-on expertise in C#, ASP.NET Core, Angular, React and database technologies.",
            Bio = "I'm a Software Engineer at Rex System Bangladesh, where I design, develop and maintain web applications with C# and ASP.NET Core. My work is mainly on backend services — RESTful APIs, JWT authentication and authorization, and database design — and I regularly build front-end features with React, Angular and Vue.js and integrate APIs with front-end and mobile teams.\n\n"
                + "Before that I was a Junior Software Engineer at Amigo Informatics, working on ASP.NET web applications, AngularJS front ends and Zoho CRM.\n\n"
                + "I hold a Bachelor of Engineering in Information and Communication Technology from Mawlana Bhashani Science and Technology University. Five years of competitive programming — including the ICPC Asia Dhaka Regional Online Preliminary and more than 1,600 solved problems — shaped how I approach problems: carefully, patiently and with an eye for efficient solutions.",
            CareerInterests = ["Backend & API development with ASP.NET Core", "Full-stack web applications", "Scalable, maintainable software design", "Database design & optimisation", "Algorithms & problem solving"],
            Highlights = ["Nearly 3 | Years of experience", "1,600+ | Problems solved", "6 | Major projects"],
            Location = "Tejgaon, Dhaka, Bangladesh",
            PublicEmail = "mdalamin18053@gmail.com",
            ShowEmail = true,
            Phone = "+8801624847416",
            ShowPhone = false,                 // Private until you choose to show it.
            WhatsAppNumber = null,             // Set in Admin → Profile to show the WhatsApp button.
            GitHubUrl = "https://github.com/it-al-amin",
            LinkedInUrl = null,                // Not in the CV — add it in Admin → Profile.
            AvailabilityStatus = "Software Engineer at Rex System Bangladesh",
            ShowAvailability = true,
            SeoTitle = "Md Al-Amin — Full-Stack Software Engineer (.NET, React)",
            SeoDescription = "Software Engineer with nearly 3 years of experience building web applications and RESTful APIs with C#, ASP.NET Core, React, Angular and SQL.",
        });

        db.EmailSettings.Add(new EmailSettings { RecipientEmail = "mdalamin18053@gmail.com", SenderName = "Md Al-Amin Portfolio" });

        db.Experiences.AddRange(
            new Experience
            {
                DisplayOrder = 0,
                Company = "Rex System Bangladesh",
                Designation = "Software Engineer",
                EmploymentType = "Full-time · Remote",
                Location = "Bangladesh",
                StartDate = new DateOnly(2024, 8, 1),
                IsCurrent = true,
                Summary = "Backend-focused engineer building web applications, RESTful APIs and a desktop document management system for client projects.",
                Responsibilities =
                [
                    "Develop robust, scalable web applications with C# and ASP.NET Core, focusing on performance.",
                    "Designed and developed RESTful API services for multiple projects, ensuring maintainability and secure client–server communication.",
                    "Applied best practices in API versioning, JWT authentication/authorization and error handling.",
                    "Built login and registration with Google and Facebook.",
                    "Develop a WinForms desktop Document Management System (DMS) in C#.",
                    "Built front-end features with Angular and Vue.js, including the IMedicine Doctor and Yoga projects.",
                    "Collaborated with front-end and mobile teams to integrate APIs and meet project requirements.",
                    "Delivered 5+ client projects using MySQL, SQL Server and SQLite.",
                ],
                Technologies = ["C#", "ASP.NET Core", "Web API", "JWT", "Entity Framework Core", "Angular", "Vue.js", "React", "WinForms", "MySQL", "SQL Server", "SQLite"],
            },
            new Experience
            {
                DisplayOrder = 1,
                Company = "Amigo Informatics",
                Designation = "Jr. Software Engineer",
                EmploymentType = "Full-time · Onsite",
                StartDate = new DateOnly(2024, 1, 1),
                EndDate = new DateOnly(2024, 8, 1),
                Responsibilities =
                [
                    "Developed and maintained web applications using C# and ASP.NET.",
                    "Worked on AngularJS and JavaScript front ends and improved application scalability.",
                    "Built UI with HTML, CSS, Bootstrap, JavaScript, AngularJS and Angular.",
                    "Worked on Zoho CRM.",
                ],
                Technologies = ["C#", "ASP.NET", "AngularJS", "Angular", "JavaScript", "Bootstrap", "Zoho CRM"],
            });

        var categories = new (string Name, string Icon, string[] Skills)[]
        {
            ("Languages", "code-slash", ["C#", "C", "C++", "Java", "JavaScript", "TypeScript", "SQL"]),
            ("Backend", "server", ["ASP.NET Core", "ASP.NET MVC", "Web API (REST)", "Entity Framework Core", "LINQ", "ADO.NET", "JWT Authentication", "WinForms"]),
            ("Frontend", "window", ["React", "Next.js", "Angular", "AngularJS", "Vue.js", "HTML5", "CSS3", "Bootstrap"]),
            ("Databases", "database", ["SQL Server", "MySQL", "SQLite"]),
            ("Tools & Platforms", "tools", ["Git", "GitHub", "MySQL Workbench", "SQL Server Management Studio", "DB Browser for SQLite", "Zoho CRM"]),
            ("Foundations", "diagram-3", ["Data Structures", "Algorithms", "Object-Oriented Programming", "Repository Pattern", "API Versioning"]),
        };
        for (var i = 0; i < categories.Length; i++)
        {
            var (name, icon, skills) = categories[i];
            db.SkillCategories.Add(new SkillCategory
            {
                Name = name, Icon = icon, DisplayOrder = i,
                Skills = skills.Select((s, j) => new Skill { Name = s, DisplayOrder = j }).ToList(),
            });
        }

        db.Projects.AddRange(
            new Project
            {
                DisplayOrder = 0, IsFeatured = true, IsPublished = true,
                Slug = "bonik-online", Title = "Bonik Online", Category = "E-Commerce & Business Management",
                Summary = "Multi-tenant e-commerce and business management platform used by multiple businesses to manage end-to-end operations.",
                Description = "A multi-tenant platform covering products, inventory, purchases, sales, customers and orders, plus accounting, transaction tracking, reporting and business analytics.",
                Technologies = ["ASP.NET Core", "Next.js", "React", "TypeScript", "MySQL"],
                Features =
                [
                    "Product, inventory, purchase, sales, customer and order management workflows",
                    "Accounting, transaction tracking, reporting and business analytics",
                    "Reusable CRUD functionality, validation and business workflows across modules",
                ],
                Responsibilities =
                [
                    "Built RESTful APIs with ASP.NET Core and integrated them with a responsive Next.js/React/TypeScript frontend.",
                    "Designed and optimized MySQL database operations, relationships and queries.",
                ],
                LiveUrl = "http://qa.bonik.online/",
            },
            new Project
            {
                DisplayOrder = 1, IsFeatured = true, IsPublished = true,
                Slug = "ccdv-climate-portal", Title = "CCDV Climate Portal", Category = "Content Management Portal",
                Summary = "Climate portal with an admin dashboard for managing content, publications, users, donations, orders and community interactions.",
                Technologies = ["ASP.NET Core", "C#", "React", "TypeScript", "MySQL", "Entity Framework Core"],
                Features =
                [
                    "Admin dashboard for website content, users, configuration, publications and community interactions",
                    "Email, donation, order, commenting and content management workflows",
                    "JWT authentication with role-based authorization",
                ],
                Responsibilities =
                [
                    "Developed RESTful APIs and business logic for users, pages, posts, publications, settings, comments, orders and donations.",
                    "Implemented JWT authentication and role-based authorization with Entity Framework Core and MySQL.",
                    "Developed responsive React/TypeScript frontend features.",
                ],
                LiveUrl = null, // The CV links "CCDB Climate Portal" without a visible URL — add it in Admin → Projects.
            },
            new Project
            {
                DisplayOrder = 2, IsFeatured = true, IsPublished = true,
                Slug = "look-space-solutions", Title = "Look Space Solutions", Category = "Corporate & Career Platform",
                Summary = "Corporate website and career management platform with dynamic job and content management.",
                Technologies = ["ASP.NET Core", "C#", "React", "TypeScript", "MySQL", "Entity Framework Core"],
                Features =
                [
                    "Job posting, online application and CV upload",
                    "Automated email notifications",
                    "Contact form and customer inquiry email workflows",
                ],
                Responsibilities =
                [
                    "Developed the corporate website and admin panel with dynamic job and content management.",
                    "Built a responsive React/TypeScript UI integrated with ASP.NET Core REST APIs and MySQL.",
                ],
                LiveUrl = "https://lookspaces.com/",
            },
            new Project
            {
                DisplayOrder = 3, IsPublished = true,
                Slug = "employee-onboarding-system", Title = "Employee Onboarding System", Category = "HR & Document Automation",
                Summary = "Secure onboarding APIs for collecting user input, processing documents and capturing signatures.",
                Technologies = ["ASP.NET Core", "C#", "RESTful APIs", "JWT", "Repository Pattern"],
                Features =
                [
                    "Automated document population from user data to reduce manual entry",
                    "Authorized document upload and signatures",
                    "Automatic email when an admin creates a new user",
                ],
                Responsibilities =
                [
                    "Developed RESTful APIs in ASP.NET Core for handling user input and processing onboarding documents.",
                    "Implemented JWT authentication for secure access and data protection.",
                    "Built the email service that notifies newly created users.",
                ],
                LiveUrl = "https://recopsys.com",
            },
            new Project
            {
                DisplayOrder = 4, IsPublished = true,
                Slug = "imedicine-doctor", Title = "I Medicine Doctor — Doctor Admin Panel", Category = "Healthcare",
                Summary = "Responsive admin panel for doctors to manage profiles, chambers, appointments, patient records and online prescriptions.",
                Technologies = ["Angular", "Bootstrap"],
                Features =
                [
                    "Create, update and delete patient and appointment data",
                    "Smart online prescription generation",
                    "Profile and chamber management",
                ],
                Responsibilities = ["Delivered a clean, responsive UI with Angular and Bootstrap."],
                LiveUrl = "https://doctor.rexsystemsbd.com",
            },
            new Project
            {
                DisplayOrder = 5, IsPublished = true,
                Slug = "document-management-system", Title = "Document Management System (DMS)", Category = "Desktop Application",
                Summary = "Windows Forms application that watches folders, captures incoming files in real time and syncs them to a central server.",
                Technologies = ["C#", "WinForms", "SQLite"],
                Features =
                [
                    "Real-time monitoring of configured folder paths",
                    "Automatic upload of captured files for centralized document management",
                    "Local SQLite storage for data and file logs",
                    "Starts automatically with Windows via Task Scheduler or Registry settings",
                ],
            });

        db.Education.Add(new Education
        {
            Degree = "Bachelor of Engineering",
            FieldOfStudy = "Information and Communication Technology",
            Institution = "Mawlana Bhashani Science and Technology University",
            EndYear = 2023,
            Grade = "CGPA 3.53 / 4.00",
        });

        db.Achievements.AddRange(
            new Achievement
            {
                DisplayOrder = 0, Kind = AchievementKind.Achievement,
                Title = "1,600+ problems solved across online judges",
                Issuer = "Competitive programming",
                Description = "GeeksforGeeks 400+, Codeforces 250+ (back_bancher.code), Beecrowd 245+ (alamin053), UVa 187+ (it_al_amin), CodeChef 110+ (md_al_amin), HackerRank 105+ (mdalamin18053), LeetCode 65+ (Md_Al_Amin), AtCoder 50+ (md_al_amin), LightOJ 37+ (mdalamin1805).",
                DateLabel = "2018 – 2023",
            },
            new Achievement
            {
                DisplayOrder = 1, Kind = AchievementKind.Achievement,
                Title = "ICPC Asia Dhaka Regional Online Preliminary Contest — Participant",
                Issuer = "ICPC · Team MBSTU_!Demons",
            },
            new Achievement
            {
                DisplayOrder = 2, Kind = AchievementKind.Achievement,
                Title = "University programming contests",
                Issuer = "Mawlana Bhashani Science and Technology University",
                Description = "Competed in the CSE-MBSTU Inter Department Programming Contest and the ICT Interdepartmental Programming Contest.",
                DateLabel = "2018 – 2023",
            });

        db.Services.AddRange(
            new ServiceOffering { DisplayOrder = 0, Icon = "hdd-network", Title = "ASP.NET Core API Development", Description = "Secure, versioned RESTful APIs with JWT authentication, role-based authorization, validation and clear error handling." },
            new ServiceOffering { DisplayOrder = 1, Icon = "window-stack", Title = "Full-Stack Web Applications", Description = "End-to-end web applications and admin dashboards with ASP.NET Core on the backend and React, Next.js or Angular on the frontend." },
            new ServiceOffering { DisplayOrder = 2, Icon = "database", Title = "Database Design", Description = "Relational schema design and query optimisation for SQL Server, MySQL and SQLite using Entity Framework Core." },
            new ServiceOffering { DisplayOrder = 3, Icon = "plug", Title = "System Integration", Description = "Social login (Google, Facebook), email services and third-party integrations, plus API integration with front-end and mobile teams." },
            new ServiceOffering { DisplayOrder = 4, Icon = "pc-display", Title = "Desktop Applications", Description = "Windows Forms applications in C#, such as background file monitoring and server synchronisation." });

        await db.SaveChangesAsync();
        await ImportSeedCvAsync(db, storage, env, logger);
        logger.LogInformation("Seeded initial portfolio content from CV data.");
    }

    /// <summary>Imports a CV placed in App_Data/seed/ (not committed to git) as the primary public CV.</summary>
    private static async Task ImportSeedCvAsync(PortfolioDbContext db, FileStorageService storage, IWebHostEnvironment env, ILogger logger)
    {
        var seedDir = Path.Combine(env.ContentRootPath, "App_Data", "seed");
        var file = Directory.Exists(seedDir) ? Directory.GetFiles(seedDir, "*.pdf").OrderBy(f => f).FirstOrDefault() : null;
        if (file is null || await db.Documents.AnyAsync()) return;

        await using var stream = File.OpenRead(file);
        var stored = await storage.SaveAsync(stream, Path.GetFileName(file), stream.Length, FileCategory.Documents);
        db.Documents.Add(new Document
        {
            Title = "Curriculum Vitae", Kind = DocumentKind.Cv, Version = "2026", IsPublic = true, IsPrimary = true,
            StoredFileName = stored.StoredFileName, OriginalFileName = "Md_Al_Amin_CV.pdf", ContentType = stored.ContentType, SizeBytes = stored.SizeBytes,
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Imported seed CV {File}.", Path.GetFileName(file));
    }
}

