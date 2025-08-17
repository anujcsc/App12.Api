using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Detect environment / hosting
var isRender = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER"));
var portEnv = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrWhiteSpace(portEnv))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{portEnv}");
}

// Resolve SQLite database path strategy prioritizing persistence on Render.
// Order:
// 1. DATABASE_PATH env
// 2. If Render & persistent disk mounted at /var/data -> /var/data/people/people.db
// 3. If /data (some custom mounts) -> /data/people/people.db
// 4. If Render but no persistent mount detected -> /tmp/people.db (ephemeral)
// 5. Local development -> ./data/people.db
string dbPath;
var dbPathEnv = Environment.GetEnvironmentVariable("DATABASE_PATH");
if (!string.IsNullOrWhiteSpace(dbPathEnv))
{
    dbPath = dbPathEnv;
}
else if (isRender)
{
    string? persistentBase = null;
    if (Directory.Exists("/var/data")) persistentBase = "/var/data"; // Render documented persistent mount
    else if (Directory.Exists("/data")) persistentBase = "/data";      // Fallback custom mount

    if (persistentBase is not null)
    {
        dbPath = Path.Combine(persistentBase, "people", "people.db");
    }
    else
    {
        dbPath = "/tmp/people.db"; // ephemeral fallback
    }
}
else
{
    var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
    Directory.CreateDirectory(dataDir);
    dbPath = Path.Combine(dataDir, "people.db");
}

// Ensure directory exists if path contains folders
try
{
    var dir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
}
catch { /* ignore */ }

// If running on Render and moving from earlier ephemeral /tmp location, migrate once
try
{
    if (isRender && dbPath.StartsWith("/var/data") || dbPath.StartsWith("/data"))
    {
        var legacy = "/tmp/people.db";
        if (File.Exists(legacy) && !File.Exists(dbPath))
        {
            var targetDir = Path.GetDirectoryName(dbPath)!;
            Directory.CreateDirectory(targetDir);
            File.Copy(legacy, dbPath);
        }
    }
}
catch { /* best effort */ }

builder.Services.AddDbContext<AppDb>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "People API", Version = "v1" });
});

// CORS config
var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
if (!string.IsNullOrWhiteSpace(allowedOrigins))
{
    var origins = allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    builder.Services.AddCors(p => p.AddDefaultPolicy(policy =>
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
}
else
{
    builder.Services.AddCors(p => p.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

app.Logger.LogInformation("Starting People API | Environment={Env} | Render={IsRender} | DbPath={DbPath}",
    app.Environment.EnvironmentName, isRender, dbPath);

// Ensure database exists (simple model; for future migrations replace with db.Database.Migrate())
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.EnsureCreated();
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));
app.MapGet("/", () => Results.Redirect("/index.html"));

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

app.MapPost("/api/people/seed", async (AppDb db) =>
{
    if (await db.People.AnyAsync())
    {
        return Results.Ok(new { added = 0, total = await db.People.CountAsync(), skipped = true });
    }
    var samples = new []
    {
        new Person { FirstName = "Ada", LastName = "Lovelace", Age = 36 },
        new Person { FirstName = "Alan", LastName = "Turing", Age = 41 },
        new Person { FirstName = "Grace", LastName = "Hopper", Age = 85 },
        new Person { FirstName = "Linus", LastName = "Torvalds", Age = 54 },
        new Person { FirstName = "Margaret", LastName = "Hamilton", Age = 87 }
    };
    db.People.AddRange(samples);
    var added = await db.SaveChangesAsync();
    return Results.Ok(new { added, total = await db.People.CountAsync(), skipped = false });
});

app.MapGet("/api/meta", () =>
{
    var ver = typeof(Program).Assembly.GetName().Version?.ToString() ?? "?";
    var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    return Results.Ok(new { version = ver, environment = env, time = DateTime.UtcNow });
});

app.Run();

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
