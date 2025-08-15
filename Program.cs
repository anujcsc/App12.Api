using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure port for Render.com
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Configure services
builder.Services.AddDbContext<AppDb>(opt => 
    opt.UseSqlite("Data Source=/tmp/people.db")); // Use /tmp for writable directory on Render

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "People API", Version = "v1" });
});

builder.Services.AddCors(p => p.AddDefaultPolicy(policy =>
{
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.EnsureCreated();
}

// Configure pipeline
app.UseCors();
app.UseDefaultFiles(); // Enables index.html discovery
app.UseStaticFiles();  // Serves files from wwwroot
app.UseSwagger();
app.UseSwaggerUI();

// Simple root endpoint (optional redundancy if index.html missing)
app.MapGet("/", () => Results.Redirect("/index.html"));

// API endpoints
app.MapGet("/api/people", async (AppDb db) =>
    await db.People.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToListAsync());

app.MapGet("/api/people/{id:int}", async (int id, AppDb db) =>
{
    var person = await db.People.FindAsync(id);
    return person is null ? Results.NotFound() : Results.Ok(person);
});

app.MapPost("/api/people", async (Person person, AppDb db) =>
{
    db.People.Add(person);
    await db.SaveChangesAsync();
    return Results.Created($"/api/people/{person.Id}", person);
});

app.MapPut("/api/people/{id:int}", async (int id, Person input, AppDb db) =>
{
    var person = await db.People.FindAsync(id);
    if (person is null) return Results.NotFound();
    
    person.FirstName = input.FirstName;
    person.LastName = input.LastName;
    person.Age = input.Age;
    
    await db.SaveChangesAsync();
    return Results.Ok(person);
});

app.MapDelete("/api/people/{id:int}", async (int id, AppDb db) =>
{
    var person = await db.People.FindAsync(id);
    if (person is null) return Results.NotFound();
    
    db.People.Remove(person);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();

// Data models
public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> options) : base(options) { }
    
    public DbSet<Person> People => Set<Person>();
}