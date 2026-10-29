using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Data;
using Portfolio.Api.Dtos;
using Portfolio.Api.Entities;

namespace Portfolio.Api.Controllers.Admin;

/// <summary>
/// Shared list/get/create/update/delete/reorder/publish endpoints for orderable portfolio content.
/// Derived controllers supply the route and can hook validation or extra loading.
/// </summary>
[ApiController]
[Authorize]
public abstract class CrudControllerBase<TEntity, TInput>(PortfolioDbContext db) : ControllerBase
    where TEntity : OrderedEntity, new()
    where TInput : IEntityInput<TEntity>
{
    protected PortfolioDbContext Db => db;
    protected virtual IQueryable<TEntity> Query => db.Set<TEntity>();

    /// <summary>Return an error message to reject the input with a 400, or null to accept it.</summary>
    protected virtual Task<string?> ValidateAsync(TInput input, TEntity? existing) => Task.FromResult<string?>(null);
    protected virtual Task BeforeSaveAsync(TEntity entity, TInput input) => Task.CompletedTask;
    protected virtual Task BeforeDeleteAsync(TEntity entity) => Task.CompletedTask;

    [HttpGet]
    public virtual async Task<ActionResult<List<TEntity>>> List() =>
        await Query.AsNoTracking().OrderBy(e => e.DisplayOrder).ThenBy(e => e.Id).ToListAsync();

    [HttpGet("{id:int}")]
    public virtual async Task<ActionResult<TEntity>> Get(int id) =>
        await Query.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id) is { } entity ? entity : NotFound();

    [HttpPost]
    public virtual async Task<ActionResult<TEntity>> Create(TInput input)
    {
        if (await ValidateAsync(input, null) is { } error) return ValidationError(error);
        var entity = new TEntity();
        input.ApplyTo(entity);
        entity.DisplayOrder = (await db.Set<TEntity>().MaxAsync(e => (int?)e.DisplayOrder) ?? -1) + 1;
        await BeforeSaveAsync(entity, input);
        db.Set<TEntity>().Add(entity);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    public virtual async Task<ActionResult<TEntity>> Update(int id, TInput input)
    {
        var entity = await Query.FirstOrDefaultAsync(e => e.Id == id);
        if (entity is null) return NotFound();
        if (await ValidateAsync(input, entity) is { } error) return ValidationError(error);
        input.ApplyTo(entity);
        await BeforeSaveAsync(entity, input);
        await db.SaveChangesAsync();
        return entity;
    }

    [HttpDelete("{id:int}")]
    public virtual async Task<IActionResult> Delete(int id)
    {
        var entity = await Query.FirstOrDefaultAsync(e => e.Id == id);
        if (entity is null) return NotFound();
        await BeforeDeleteAsync(entity);
        db.Set<TEntity>().Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Sets DisplayOrder from the position of each id in the list. Ids not in the list keep their order after the listed ones.</summary>
    [HttpPut("reorder")]
    public virtual async Task<IActionResult> Reorder(ReorderRequest request)
    {
        var items = await db.Set<TEntity>().Where(e => request.Ids.Contains(e.Id)).ToListAsync();
        if (items.Count != request.Ids.Distinct().Count()) return ValidationError("One or more items no longer exist. Refresh and try again.");
        for (var i = 0; i < request.Ids.Count; i++) items.First(e => e.Id == request.Ids[i]).DisplayOrder = i;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/publish")]
    public virtual async Task<IActionResult> SetPublished(int id, PublishRequest request)
    {
        var entity = await db.Set<TEntity>().FindAsync(id);
        if (entity is null) return NotFound();
        entity.IsPublished = request.IsPublished;
        await db.SaveChangesAsync();
        return NoContent();
    }

    protected ActionResult ValidationError(string message) =>
        ValidationProblem(new ValidationProblemDetails { Detail = message, Title = message });
}
