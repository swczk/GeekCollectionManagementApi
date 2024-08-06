using System.IdentityModel.Tokens.Jwt;
using AspNetCore.SwaggerUI.Themes;
using System.Security.Claims;
using System.Text;
using Domain.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Web.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<DBContext>();

// JWT Authentication Configuration
var keyBytes = Encoding.UTF8.GetBytes(builder.Configuration.GetSection("AppSettings:Token").Value!);

builder.Services.AddAuthentication(options =>
{
   options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
   options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
   options.TokenValidationParameters = new TokenValidationParameters
   {
      ValidateIssuerSigningKey = true,
      ValidateIssuer = false,
      ValidateAudience = false,
      IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
   };
});

builder.Services.AddAuthorization();

// Swagger Configuration
builder.Services.AddSwaggerGen(c =>
{
   c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
   {
      Description = "JWT Authorization header usando o esquema Bearer. \r\n\r\n" +
                 "Digite 'Bearer' [espaço] e então seu token na entrada de texto abaixo.\r\n\r\n" +
                 "Exemplo: 'Bearer 12345abcdef'",
      Name = "Authorization",
      In = ParameterLocation.Header,
      Type = SecuritySchemeType.ApiKey,
      Scheme = "Bearer"
   });
   c.AddSecurityRequirement(new OpenApiSecurityRequirement()
   {
      {
         new OpenApiSecurityScheme
         {
            Reference = new OpenApiReference
            {
               Type = ReferenceType.SecurityScheme,
               Id = "Bearer"
            },
            Scheme = "oauth2",
            Name = "Bearer",
            In = ParameterLocation.Header,
         },
         new List<string>()
      }
   });
});

builder.Host.UseSerilog((context, loggerConfiguration) =>
   loggerConfiguration.ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
   app.UseSwagger();
   app.UseSwaggerUI(Style.Dark);
}

app.UseSerilogRequestLogging();

app.UseRouting();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/login", async (LoginModel userLogin, DBContext db) =>
{
   var user = await db.Users.FirstOrDefaultAsync(u => u.Email == userLogin.Email);

   if (user == null || !BCrypt.Net.BCrypt.Verify(userLogin.Password, user.Password))
   {
      return Results.Unauthorized();
   }

   var tokenHandler = new JwtSecurityTokenHandler();
   var tokenDescriptor = new SecurityTokenDescriptor
   {
      Subject = new ClaimsIdentity(new[]
      {
         new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
         new Claim(ClaimTypes.Email, user.Email)
      }),
      Expires = DateTime.UtcNow.AddHours(1),
      SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
   };

   var token = tokenHandler.CreateToken(tokenDescriptor);
   var tokenString = tokenHandler.WriteToken(token);

   return Results.Ok(new { Token = tokenString });
})
.WithName("Login")
.WithOpenApi();

app.MapPost("/register", async (RegisterModel userRegister, DBContext db) =>
{
   var existingUser = await db.Users.FirstAsync(u => u.Email == userRegister.Email);

   if (existingUser != null)
   {
      return Results.Conflict("Usuário já existe.");
   }

   var hashedPassword = BCrypt.Net.BCrypt.HashPassword(userRegister.Password);

   var newUser = new User
   {
      Username = userRegister.Username,
      Email = userRegister.Email,
      Password = hashedPassword,
   };

   db.Users.Add(newUser);
   await db.SaveChangesAsync();

   return Results.Ok("Usuário registrado com sucesso.");
})
.WithName("RegisterUser")
.WithOpenApi();

app.MapGet("/verify", [Authorize] () =>
{
   return Results.Ok();
})
.WithName("VerifyToken")
.WithOpenApi();

// Collections Endpoints
app.MapGet("/collections", [Authorize] async (DBContext db, HttpContext httpContext) =>
{
   if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value, out var userId))
   {
      return Results.Unauthorized();
   }

   var collections = await db.Collections
      .Where(c => c.UserId == userId)
      .ToListAsync();

   return Results.Ok(collections);
})
.WithName("GetAllCollections")
.WithOpenApi();

app.MapGet("/collections/{id}", [Authorize] async (int id, DBContext db, HttpContext httpContext) =>
{
   if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value, out var userId))
   {
      return Results.Unauthorized();
   }

   var collection = await db.Collections
      .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

   if (collection == null)
   {
      return Results.NotFound();
   }

   return Results.Ok(collection);
})
.WithName("GetCollectionById")
.WithOpenApi();

app.MapPost("/collections", [Authorize] async (Collection collection, DBContext db, HttpContext httpContext) =>
{
   if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value, out var userId))
   {
      return Results.Unauthorized();
   }

   collection.UserId = userId;

   db.Collections.Add(collection);
   await db.SaveChangesAsync();

   return Results.Created($"/collections/{collection.Id}", collection);
})
.WithName("CreateCollection")
.WithOpenApi();

app.MapPut("/collections/{id}", [Authorize] async (int id, Collection updatedCollection, DBContext db, HttpContext httpContext) =>
{
   if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value, out var userId))
   {
      return Results.Unauthorized();
   }

   var collection = await db.Collections
      .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

   if (collection == null)
   {
      return Results.NotFound();
   }

   collection.Name = updatedCollection.Name;
   collection.Description = updatedCollection.Description;

   db.Collections.Update(collection);
   await db.SaveChangesAsync();

   return Results.NoContent();
})
.WithName("UpdateCollection")
.WithOpenApi();

app.MapDelete("/collections/{id}", [Authorize] async (int id, DBContext db, HttpContext httpContext) =>
{
   if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value, out var userId))
   {
      return Results.Unauthorized();
   }

   var collection = await db.Collections
      .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

   if (collection == null)
   {
      return Results.NotFound();
   }

   db.Collections.Remove(collection);
   await db.SaveChangesAsync();

   return Results.NoContent();
})
.WithName("DeleteCollection")
.WithOpenApi();

// Items Endpoints
app.MapGet("/collections/{collectionId}/items", [Authorize] async (int collectionId, DBContext db, HttpContext httpContext) =>
{
   if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value, out var userId))
   {
      return Results.Unauthorized();
   }

   var items = await db.Items
      .Where(i => i.CollectionId == collectionId && i.Collection.UserId == userId)
      .ToListAsync();

   return Results.Ok(items);
})
.WithName("GetAllItems")
.WithOpenApi();

app.MapGet("/collections/{collectionId}/items/{itemId}", [Authorize] async (int collectionId, int itemId, DBContext db, HttpContext httpContext) =>
{
   if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value, out var userId))
   {
      return Results.Unauthorized();
   }

   var item = await db.Items
      .FirstOrDefaultAsync(i => i.Id == itemId && i.CollectionId == collectionId && i.Collection.UserId == userId);

   if (item == null)
   {
      return Results.NotFound();
   }

   return Results.Ok(item);
})
.WithName("GetItemById")
.WithOpenApi();

app.MapPost("/collections/{collectionId}/items", [Authorize] async (int collectionId, Item item, DBContext db, HttpContext httpContext) =>
{
   if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value, out var userId))
   {
      return Results.Unauthorized();
   }

   var collection = await db.Collections
      .FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId);

   if (collection == null)
   {
      return Results.NotFound();
   }

   item.CollectionId = collectionId;

   db.Items.Add(item);
   await db.SaveChangesAsync();

   return Results.Created($"/collections/{collectionId}/items/{item.Id}", item);
})
.WithName("CreateItem")
.WithOpenApi();

app.MapPut("/collections/{collectionId}/items/{itemId}", [Authorize] async (int collectionId, int itemId, Item updatedItem, DBContext db, HttpContext httpContext) =>
{
   if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value, out var userId))
   {
      return Results.Unauthorized();
   }

   var item = await db.Items
      .FirstOrDefaultAsync(i => i.Id == itemId && i.CollectionId == collectionId && i.Collection.UserId == userId);

   if (item == null)
   {
      return Results.NotFound();
   }

   item.Name = updatedItem.Name;
   item.Description = updatedItem.Description;

   db.Items.Update(item);
   await db.SaveChangesAsync();

   return Results.NoContent();
})
.WithName("UpdateItem")
.WithOpenApi();

app.MapDelete("/collections/{collectionId}/items/{itemId}", [Authorize] async (int collectionId, int itemId, DBContext db, HttpContext httpContext) =>
{
   if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value, out var userId))
   {
      return Results.Unauthorized();
   }

   var item = await db.Items
      .FirstOrDefaultAsync(i => i.Id == itemId && i.CollectionId == collectionId && i.Collection.UserId == userId);

   if (item == null)
   {
      return Results.NotFound();
   }

   db.Items.Remove(item);
   await db.SaveChangesAsync();

   return Results.NoContent();
})
.WithName("DeleteItem")
.WithOpenApi();


app.Run();
