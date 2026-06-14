using Microsoft.EntityFrameworkCore;
using MinimalConsumer.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<MinimalDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddAuthentication("Cookies").AddCookie("Cookies");
builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Widgets}/{action=Index}/{id?}");

app.Run();

// Expose Program for WebApplicationFactory<Program> in tests
public partial class Program { }
