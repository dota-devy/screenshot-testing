using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinimalConsumer.Data;

namespace MinimalConsumer.Controllers;

public class WidgetsController(IDbContextFactory<MinimalDbContext> dbFactory) : Controller
{
    [HttpGet("/")]
    [HttpGet("/widgets")]
    public async Task<IActionResult> Index()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var widgets = await db.Widgets.OrderBy(w => w.Name).ToListAsync();
        return View(widgets);
    }

    [HttpGet("/widgets/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var widget = await db.Widgets.FirstOrDefaultAsync(w => w.Id == id);
        if (widget is null) return NotFound();
        return View(widget);
    }

    [Authorize]
    [HttpGet("/widgets/mine")]
    public async Task<IActionResult> Mine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await using var db = await dbFactory.CreateDbContextAsync();
        var widgets = await db.Widgets
            .Where(w => w.OwnerUserId == userId)
            .OrderBy(w => w.Name)
            .ToListAsync();
        return View(widgets);
    }
}
