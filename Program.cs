using System.Text;
using BambooBrain_Service.Helpers;
using BambooBrain_Service.Repositories.Users;
using BambooBrain_Service.Services.Auth;
using Microsoft.Azure.Cosmos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using BambooBrain_Service.Repositories.Documents;
using BambooBrain_Service.Services.BlobStorage;
using BambooBrain_Service.Services.Document;
using BambooBrain_Service.Services.Extraction;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;
using BambooBrain_Service.Repositories.Flashcards;
using BambooBrain_Service.Services.Flashcard;
using BambooBrain_Service.Repositories.Quiz;
using BambooBrain_Service.Services.Quiz;
using BambooBrain_Service.Repositories.Speaking;
using BambooBrain_Service.Services.Speaking;
using BambooBrain_Service.Repositories.Planner;
using BambooBrain_Service.Repositories.Stats;
using BambooBrain_Service.Services.Planner;
using BambooBrain_Service.Repositories.Notifications;
using BambooBrain_Service.Services.Notifications;
using BambooBrain_Service.Services.Settings;
using BambooBrain_Service.Services.Search;

var builder = WebApplication.CreateBuilder(args);

// Cosmos DB
builder.Services.AddSingleton(_ =>
    new CosmosClient(builder.Configuration["Cosmos:ConnectionString"]));

// App services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<JwtHelper>();

// JWT auth
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };
    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:3000", "https://bamboo-brain-pavilion.vercel.app", "https://bamboo-brain-pavilion-guayavazezacgdhr.southeastasia-01.azurewebsites.net")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// Blob Storage
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();

// Document feature
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IExtractionService, DocumentExtractionService>();

builder.Services.AddScoped<ISettingsService, SettingsService>();

// ↓ ADD THESE TWO LINES
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();

// Allow large file uploads
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104_857_600; // 100MB
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// Required for IFormFile to work correctly
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104_857_600; // 100MB
});

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token here"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHttpClient("SpeechApi");
builder.Services.AddScoped<AudioExtractionService>();

builder.Services.AddHttpClient("VideoIndexer");
builder.Services.AddScoped<VideoExtractionService>();

builder.Services.AddScoped<IFlashcardRepository, FlashcardRepository>();
builder.Services.AddScoped<IFlashcardService, FlashcardService>();

builder.Services.AddScoped<IQuizRepository, QuizRepository>();
builder.Services.AddScoped<IQuizService, QuizService>();

builder.Services.AddScoped<ISpeakingRepository, SpeakingRepository>();
builder.Services.AddScoped<ISpeechService, SpeechService>();
builder.Services.AddScoped<ISpeakingService, SpeakingService>();

builder.Services.AddScoped<IPlannerRepository, PlannerRepository>();
builder.Services.AddScoped<IStatsRepository, StatsRepository>();
builder.Services.AddScoped<GoalAgent>();
builder.Services.AddScoped<MonitorAgent>();
builder.Services.AddScoped<AdaptAgent>();
builder.Services.AddScoped<IPlannerService, PlannerService>();

builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddSingleton<SearchIndexSetup>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IAISearchService, AISearchService>();
builder.Services.AddScoped<IRagChatService, RagChatService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var indexSetup = scope.ServiceProvider.GetRequiredService<SearchIndexSetup>();
    await indexSetup.EnsureIndexesExistAsync();
}

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
