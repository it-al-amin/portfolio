using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Data;
using Portfolio.Api.Dtos;
using Portfolio.Api.Entities;

namespace Portfolio.Api.Controllers.Admin;

[Route("api/admin/experiences")]
public class ExperiencesController(PortfolioDbContext db) : CrudControllerBase<Experience, ExperienceInput>(db);

[Route("api/admin/education")]
public class EducationController(PortfolioDbContext db) : CrudControllerBase<Education, EducationInput>(db);

[Route("api/admin/services")]
public class ServicesController(PortfolioDbContext db) : CrudControllerBase<ServiceOffering, ServiceInput>(db);

[Route("api/admin/skill-categories")]
public class SkillCategoriesController(PortfolioDbContext db) : CrudControllerBase<SkillCategory, SkillCategoryInput>(db);

[Route("api/admin/skills")]
public class SkillsController(PortfolioDbContext db) : CrudControllerBase<Skill, SkillInput>(db)
{
    protected override async Task<string?> ValidateAsync(SkillInput input, Skill? existing) =>
        await Db.SkillCategories.AnyAsync(c => c.Id == input.CategoryId) ? null : "The selected category does not exist.";
}

[Route("api/admin/achievements")]
public class AchievementsController(PortfolioDbContext db) : CrudControllerBase<Achievement, AchievementInput>(db)
{
    protected override async Task<string?> ValidateAsync(AchievementInput input, Achievement? existing) =>
        input.DocumentId is { } docId && !await Db.Documents.AnyAsync(d => d.Id == docId) ? "The linked document does not exist." : null;
}
