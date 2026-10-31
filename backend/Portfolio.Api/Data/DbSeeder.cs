using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Entities;
using Portfolio.Api.Services;

namespace Portfolio.Api.Data;

/// <summary>
/// One-time initial content taken from Md Al-Amin's CV ("Md-Al-Amin-CV-updated.pdf") and the Amigo Informatics
/// experience certificate (dated 31/08/2024). Runs only when the database has no profile,
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
            Headline = "Software Engineer",
            Tagline = ".NET · ASP.NET Core · React · Angular — building secure, scalable web applications and APIs.",
            Summary = "Software Engineer with nearly 3 years of experience designing and building secure, scalable web applications and RESTful APIs with C#, ASP.NET Core and Entity Framework Core, and responsive front ends in React, Next.js and Angular. Delivered 5+ client projects across e-commerce, healthcare, HR and content management on SQL Server, MySQL and SQLite, and currently build the procurement module of an enterprise ERP on Oracle.",
            Bio = "I'm a Software Engineer at Rex System Bangladesh, where I design and develop scalable web applications and RESTful APIs in C# and ASP.NET Core — JWT authentication, role-based authorization, API versioning and consistent error handling across multiple client projects — and build front-end features in Angular and Vue.js.\n\n"
                + "Since September 2026 I also work on-site with the IT department of Akij Insaf Group on AiPRO ERP, the group's in-house enterprise platform for textile manufacturing, where I own the procurement (P2P) module APIs end to end on Oracle.\n\n"
                + "Before that I was a Junior Software Engineer at Amigo Informatics, working on ASP.NET web applications, AngularJS and Angular front ends and Zoho CRM.\n\n"
                + "I hold a Bachelor of Engineering in Information and Communication Technology from Mawlana Bhashani Science and Technology University. Five years of competitive programming — including the ICPC Asia Dhaka Regional Online Preliminary and more than 1,600 solved problems — shaped how I approach problems: carefully, patiently and with an eye for efficient solutions.",
            CareerInterests = ["Backend & API development with ASP.NET Core", "Enterprise systems & approval workflows", "Full-stack web applications", "Clean, maintainable software architecture", "Database design & optimisation", "Algorithms & problem solving"],
            Highlights = ["Nearly 3 | Years of experience", "5+ | Client projects delivered", "1,600+ | Problems solved"],
            Location = "Tejgaon, Dhaka, Bangladesh",
            PublicEmail = "mdalamin18053@gmail.com",
            ShowEmail = true,
            Phone = "+8801624847416",
            ShowPhone = false,                 // Private until you choose to show it.
            WhatsAppNumber = null,             // Set in Admin → Profile to show the WhatsApp button.
            GitHubUrl = "https://github.com/it-al-amin",
            LinkedInUrl = "https://www.linkedin.com/in/al-amin53",
            AvailabilityStatus = "Software Engineer at Rex System Bangladesh & Akij Insaf Group",
            ShowAvailability = true,
            SeoTitle = "Md Al-Amin — Software Engineer (.NET, ASP.NET Core, React, Angular)",
            SeoDescription = "Software Engineer with nearly 3 years of experience building secure web applications, RESTful APIs and enterprise ERP modules with C#, ASP.NET Core, EF Core, React, Angular, SQL Server, MySQL and Oracle.",
        });

        db.EmailSettings.Add(new EmailSettings { RecipientEmail = "mdalamin18053@gmail.com", SenderName = "Md Al-Amin Portfolio" });

        db.Experiences.AddRange(
            new Experience
            {
                DisplayOrder = 0,
                Company = "Akij Insaf Group, IT Department",
                Designation = "Software Engineer",
                EmploymentType = "Contractual · On-site · Project-based",
                Location = "Dhaka, Bangladesh",
                StartDate = new DateOnly(2026, 9, 1),
                IsCurrent = true,
                Summary = "Backend features for AiPRO ERP, the group's in-house enterprise platform for textile manufacturing, working on-site with the IT department.",
                Responsibilities =
                [
                    "Own the procurement (P2P) module APIs end to end: entities, EF Core migrations on Oracle, services, validation and role-based endpoint permissions.",
                    "Build multi-step approval workflows, revision history with version restore, sequential document codes, file attachments and printable PDF reports with QuestPDF.",
                    "Resolve QA issues sprint by sprint and support front-end developers with written API integration guides.",
                ],
                Technologies = ["C#", "ASP.NET Core", "Entity Framework Core", "Oracle", "QuestPDF", "Clean Architecture"],
            },
            new Experience
            {
                DisplayOrder = 1,
                Company = "Rex System Bangladesh",
                Designation = "Software Engineer",
                EmploymentType = "Full-time · Remote",
                Location = "Bangladesh",
                StartDate = new DateOnly(2024, 8, 1),
                IsCurrent = true,
                Summary = "Design and develop scalable web applications and RESTful APIs in C# and ASP.NET Core, focused on performance, maintainability and secure client–server communication.",
                Responsibilities =
                [
                    "Implement JWT authentication, role-based authorization, API versioning and consistent error handling across multiple client projects.",
                    "Integrated Google and Facebook social login into user registration and sign-in flows.",
                    "Develop a C# WinForms Document Management System that monitors folders in real time and syncs captured files to a central server.",
                    "Build front-end features in Angular and Vue.js, including the I Medicine Doctor admin panel and a yoga platform.",
                    "Collaborate with front-end and mobile teams to integrate APIs and deliver against client requirements.",
                    "Delivered 5+ client projects using MySQL, SQL Server and SQLite.",
                ],
                Technologies = ["C#", "ASP.NET Core", "Web API", "JWT", "Entity Framework Core", "Angular", "Vue.js", "React", "WinForms", "MySQL", "SQL Server", "SQLite"],
            },
            new Experience
            {
                DisplayOrder = 2,
                Company = "Amigo Informatics",
                Designation = "Junior Software Engineer",
                EmploymentType = "Full-time · On-site",
                Location = "Uttara, Dhaka, Bangladesh",
                StartDate = new DateOnly(2024, 1, 22),   // Dates from the experience certificate (22 Jan – 31 Aug 2024).
                EndDate = new DateOnly(2024, 8, 31),
                Responsibilities =
                [
                    "Developed and maintained web applications using C# and ASP.NET.",
                    "Built responsive user interfaces with HTML, CSS, Bootstrap, JavaScript, AngularJS and Angular, improving front-end scalability.",
                    "Worked with Zoho CRM to support client business processes.",
                ],
                Technologies = ["C#", "ASP.NET", "AngularJS", "Angular", "JavaScript", "Bootstrap", "Zoho CRM"],
            });

        var categories = new (string Name, string Icon, string[] Skills)[]
        {
            ("Languages", "code-slash", ["C#", "JavaScript", "TypeScript", "SQL", "C", "C++", "Java"]),
            ("Backend", "server", ["ASP.NET Core", "ASP.NET MVC", "RESTful Web API", "Entity Framework Core", "LINQ", "ADO.NET", "JWT Authentication", "WinForms", "QuestPDF"]),
            ("Frontend", "window", ["React", "Next.js", "Angular", "AngularJS", "Vue.js", "HTML5", "CSS3", "Bootstrap"]),
            ("Databases", "database", ["Oracle", "SQL Server", "MySQL", "SQLite"]),
            ("Practices", "diagram-3", ["Clean Architecture", "Object-Oriented Design", "Repository Pattern", "Approval Workflows", "API Versioning", "Role-Based Authorization", "Data Structures & Algorithms"]),
            ("Tools", "tools", ["Git", "GitHub", "SQL Server Management Studio", "MySQL Workbench", "DB Browser for SQLite", "Zoho CRM"]),
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
                Slug = "aipro-erp-procurement", Title = "AiPRO ERP — Procurement Module", Category = "Enterprise ERP",
                Summary = "Procurement (P2P) module of Akij Insaf Group's in-house enterprise ERP for textile manufacturing.",
                Description = "End-to-end procurement APIs covering contracts and pre-approved rates, import and local purchase orders, LC preparation, shipments, insurance, customs, document release and payment maturity.",
                Role = "Software Engineer — owner of the procurement module APIs",
                Technologies = ["ASP.NET Core", "C#", "Entity Framework Core", "Oracle", "QuestPDF", "Clean Architecture"],
                Features =
                [
                    "Contracts and pre-approved rates, import and local purchase orders, LC preparation and shipments",
                    "Insurance cover notes and policies, custom duty and taxes, document release and payment maturity",
                    "Import-cost modules (BBLC, depot and C&F charges) and the C&F Module Entry for bulk delivery to C&F agents",
                    "Multi-step approval workflows, revision history with version restore, sequential document codes and file attachments",
                    "Printable PDF reports with QuestPDF",
                ],
                Responsibilities =
                [
                    "Own the procurement APIs end to end: entities, EF Core migrations on Oracle, services, validation and role-based endpoint permissions.",
                    "Resolve QA issues sprint by sprint and write API integration guides for front-end developers.",
                ],
                LiveUrl = "http://84.247.144.67/login",
            },
            new Project
            {
                DisplayOrder = 1, IsFeatured = true, IsPublished = true,
                Slug = "emergency-care-services", Title = "Emergency & Care Services", Category = "Healthcare Platform",
                Summary = "Bengali/English platform that helps families in Bangladesh, especially in towns and villages, request verified nurse home visits, patient transport and elder care.",
                Description = "Staff coordinators triage each request, assign an approved local provider and track it through to payment (cash, bKash or Nagad). Life-threatening cases are handed off to 999/16263 and never dispatched through the platform. A pilot is planned for Netrokona and Mymensingh, with admin-controlled area rollout and a per-request service fee.",
                Technologies = [".NET 10", "ASP.NET Core", "Entity Framework Core", "MySQL", "React", "TypeScript", "Vite", "JWT", "Docker", "GitHub Actions"],
                Features =
                [
                    "Bengali and English interface with area search and \"find areas near me\"",
                    "Request flow for nurse home visits, patient transport and elder care, with public request tracking",
                    "Coordinator triage and assignment of approved local providers, tracked through to payment (cash, bKash, Nagad)",
                    "Emergency safeguard: life-threatening cases are directed to 999/16263 and never dispatched",
                    "Geography-based area availability with admin-controlled rollout",
                ],
                Responsibilities =
                [
                    "Designed a modular-monolith ASP.NET Core backend with EF Core and MySQL, and a request state machine for the full service lifecycle.",
                    "Implemented JWT and Google sign-in, role-based access, configurable SMS notifications and rate limiting.",
                    "Built the React + TypeScript + Vite front end.",
                    "Set up Docker and GitHub Actions CI with domain and integration tests.",
                ],
                LiveUrl = "https://emergency-care-service.vercel.app/",
            },
            new Project
            {
                DisplayOrder = 2, IsFeatured = true, IsPublished = true,
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
                    "Built ASP.NET Core RESTful APIs for products, inventory, purchases, sales, orders, accounting and reporting, integrated with a Next.js/React/TypeScript front end.",
                    "Designed and optimized MySQL schemas, relationships and queries; built reusable CRUD, validation and workflow components across modules.",
                ],
                LiveUrl = "https://prod.bonik.online/",
            },
            new Project
            {
                DisplayOrder = 3, IsFeatured = true, IsPublished = true,
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
                DisplayOrder = 4, IsFeatured = true, IsPublished = true,
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
                DisplayOrder = 5, IsPublished = true,
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
                DisplayOrder = 6, IsPublished = true,
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
                DisplayOrder = 7, IsPublished = true,
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
                Description = "Competed in the CSE-MBSTU Inter-Department and ICT Interdepartmental Programming Contests.",
                DateLabel = "2018 – 2023",
            },
            new Achievement
            {
                // Upload the certificate PDF in Admin → Documents (type Certificate) and link it here to offer a download.
                DisplayOrder = 3, Kind = AchievementKind.Certification,
                Title = "Experience Certificate — Jr. Software Engineer",
                Issuer = "Amigo Informatics",
                Description = "Certifies employment as Jr. Software Engineer from 22 January 2024 to 31 August 2024, describing professional, hard-working and result-oriented work and excellent team behaviour.",
                Date = new DateOnly(2024, 8, 31),
            });

        db.Services.AddRange(
            new ServiceOffering { DisplayOrder = 0, Icon = "hdd-network", Title = "ASP.NET Core API Development", Description = "Secure, versioned RESTful APIs with JWT authentication, role-based authorization, validation and clear error handling." },
            new ServiceOffering { DisplayOrder = 1, Icon = "window-stack", Title = "Full-Stack Web Applications", Description = "End-to-end web applications and admin dashboards with ASP.NET Core on the backend and React, Next.js or Angular on the frontend." },
            new ServiceOffering { DisplayOrder = 2, Icon = "diagram-3", Title = "Enterprise & ERP Modules", Description = "Business modules with multi-step approval workflows, revision history, role-based permissions and printable PDF reports, built with Clean Architecture." },
            new ServiceOffering { DisplayOrder = 3, Icon = "database", Title = "Database Design", Description = "Relational schema design, EF Core migrations and query optimisation for Oracle, SQL Server, MySQL and SQLite." },
            new ServiceOffering { DisplayOrder = 4, Icon = "plug", Title = "System Integration", Description = "Social login (Google, Facebook), email services and third-party integrations, plus API integration with front-end and mobile teams." },
            new ServiceOffering { DisplayOrder = 5, Icon = "pc-display", Title = "Desktop Applications", Description = "Windows Forms applications in C#, such as background file monitoring and server synchronisation." });

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

