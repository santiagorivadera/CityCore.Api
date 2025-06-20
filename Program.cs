using System;
using System.Text;
using CityCore.Api.Services;
using CityCore.Data;
using Microsoft.EntityFrameworkCore;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;

var builder = WebApplication.CreateBuilder(args);

// 1) Blob Storage
var blobSection = builder.Configuration.GetSection("AzureBlobStorage");
var blobConn    = blobSection["ConnectionString"]!;
var container   = blobSection["ContainerName"]!;

builder.Services.AddSingleton(_ => new BlobServiceClient(blobConn));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<BlobServiceClient>()
      .GetBlobContainerClient(container)
);

// 2) DbContext
builder.Services.AddDbContext<CityCoreContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// 3) CORS — Ahora permitimos tanto al front (3000) como al Swagger/localhost:5096
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontAndSwagger", policy =>
        policy
          .WithOrigins(
            "http://localhost:3000",  // <— tu front Next/React
            "http://localhost:5096"   // <— swagger o cualquier otra
          )
          .AllowAnyHeader()
          .AllowAnyMethod()
    );
});

// 4) JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var key        = Encoding.UTF8.GetBytes(jwtSection["Key"]!);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = jwtSection["Issuer"],
            ValidateAudience         = true,
            ValidAudience            = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(key),
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.Zero,
            NameClaimType            = JwtRegisteredClaimNames.Sub
        };
    });

builder.Services.AddAuthorization();

// 5) Controllers + Swagger + HostedService
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<VotacionClosureService>();

var app = builder.Build();

// 6) Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// **Importante**: CORS antes de Authentication/Authorization y MapControllers
app.UseCors("AllowFrontAndSwagger");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
