# People API - Render.com Deployment

A simple .NET 9 ASP.NET Core API for managing people data with a web frontend.

## Features
- Full CRUD operations for people (Create, Read, Update, Delete)
- SQLite database
- Swagger/OpenAPI documentation
- Web frontend UI
- CORS enabled for frontend integration

## Local Development

```bash
dotnet run
```

Visit: http://localhost:5000

## Deployment to Render.com

### Option 1: Using Render Dashboard
1. Push your code to GitHub/GitLab
2. Go to [Render.com](https://render.com) and sign up/login
3. Click "New +" ? "Web Service"
4. Connect your repository
5. Configure:
   - **Name**: `people-api` (or any name you prefer)
   - **Environment**: `Docker`
   - **Plan**: `Free` (or paid for better performance)
   - **Dockerfile Path**: `./Dockerfile`
6. Click "Create Web Service"

### Option 2: Using render.yaml (Infrastructure as Code)
1. Push code with `render.yaml` to your repository
2. In Render dashboard, click "New +" ? "Blueprint"
3. Connect your repository
4. Render will automatically read the `render.yaml` configuration

### Important Notes for Render Deployment:
- **Database**: Uses SQLite stored in `/tmp` (ephemeral storage)
- **Port**: Automatically configured via `PORT` environment variable
- **Free Tier**: Service will sleep after 15 minutes of inactivity
- **Persistent Storage**: For production, consider upgrading to paid plan and using PostgreSQL

### After Deployment:
Your API will be available at: `https://your-app-name.onrender.com`

API Endpoints:
- `GET /api/people` - List all people
- `GET /api/people/{id}` - Get person by ID
- `POST /api/people` - Create new person
- `PUT /api/people/{id}` - Update person
- `DELETE /api/people/{id}` - Delete person
- `GET /swagger` - API documentation

Web Frontend: `https://your-app-name.onrender.com/`

## Environment Variables (Optional)
- `ASPNETCORE_ENVIRONMENT`: Set to `Production` for production deployment
- `PORT`: Automatically set by Render (defaults to 8080)

## File Structure
```
??? Program.cs              # Main application entry point
??? App12.Api.csproj       # Project file
??? wwwroot/
?   ??? index.html         # Web frontend
??? Dockerfile             # Docker configuration for Render
??? .dockerignore          # Files to exclude from Docker build
??? render.yaml            # Render deployment configuration
??? README.md              # This file
```