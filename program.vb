Imports Microsoft.EntityFrameworkCore
Imports Microsoft.OpenApi.Models
Imports System.Linq
' ASP.NET Core namespaces
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.DependencyInjection

Public Class Person
    Public Property Id As Integer
    Public Property FirstName As String = String.Empty
    Public Property LastName As String = String.Empty
    Public Property Age As Integer
End Class

Public Class AppDb
    Inherits DbContext

    Public Sub New(options As DbContextOptions(Of AppDb))
        MyBase.New(options)
    End Sub

    Public Property People As DbSet(Of Person)
End Class

Module Program
    Public Sub Main(args As String())
        Dim builder = WebApplication.CreateBuilder(args)

        ' Render.com hosting: bind to PORT env on 0.0.0.0 so platform health checks succeed.
        Dim port = Environment.GetEnvironmentVariable("PORT")
        If String.IsNullOrWhiteSpace(port) Then
            port = "8080" ' default for local run when mimicking Render
        End If
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}")

        ' Use /tmp for SQLite on Render (ephemeral) otherwise local file.
        Dim dbPath As String = "people.db"
        If Not String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER")) OrElse Not String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PORT")) Then
            dbPath = "/tmp/people.db"
        End If

        builder.Services.AddDbContext(Of AppDb)(
            Sub(opt) opt.UseSqlite($"Data Source={dbPath}")) ' Local file in working directory

        builder.Services.AddEndpointsApiExplorer()
        builder.Services.AddSwaggerGen(
            Sub(c)
                c.SwaggerDoc("v1", New OpenApiInfo With {
                    .Title = "People API",
                    .Version = "v1"
                })
            End Sub)

        ' Allow all origins (sufficient for demo). Tighten for production.
        builder.Services.AddCors(
            Sub(p)
                p.AddDefaultPolicy(
                    Sub(policy)
                        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
                    End Sub)
            End Sub)

        Dim app = builder.Build()

        ' Auto-create database (simple demo; migrations recommended for real apps)
        Using scope = app.Services.CreateScope()
            Dim db = scope.ServiceProvider.GetRequiredService(Of AppDb)()
            db.Database.EnsureCreated()
        End Using

        app.UseCors()
        app.UseDefaultFiles()
        app.UseStaticFiles()
        app.UseSwagger()
        app.UseSwaggerUI()

        app.MapGet("/",
            Function() Results.Redirect("/index.html"))

        app.MapGet("/api/people",
            Async Function(db As AppDb)
                Return Results.Ok(Await db.People _
                    .OrderBy(Function(p) p.LastName) _
                    .ThenBy(Function(p) p.FirstName) _
                    .ToListAsync())
            End Function)

        app.MapGet("/api/people/{id:int}",
            Async Function(id As Integer, db As AppDb)
                Dim person = Await db.People.FindAsync(id)
                If person Is Nothing Then Return Results.NotFound()
                Return Results.Ok(person)
            End Function)

        app.MapPost("/api/people",
            Async Function(person As Person, db As AppDb)
                db.People.Add(person)
                Await db.SaveChangesAsync()
                Return Results.Created($"/api/people/{person.Id}", person)
            End Function)

        app.MapPut("/api/people/{id:int}",
            Async Function(id As Integer, input As Person, db As AppDb)
                Dim person = Await db.People.FindAsync(id)
                If person Is Nothing Then Return Results.NotFound()
                person.FirstName = input.FirstName
                person.LastName = input.LastName
                person.Age = input.Age
                Await db.SaveChangesAsync()
                Return Results.Ok(person)
            End Function)

        app.MapDelete("/api/people/{id:int}",
            Async Function(id As Integer, db As AppDb)
                Dim person = Await db.People.FindAsync(id)
                If person Is Nothing Then Return Results.NotFound()
                db.People.Remove(person)
                Await db.SaveChangesAsync()
                Return Results.NoContent()
            End Function)

        app.Run()
    End Sub
End Module