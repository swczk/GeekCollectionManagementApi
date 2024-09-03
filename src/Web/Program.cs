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
using Microsoft.AspNetCore.Mvc;
using Application.DTO;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<DBContext>();

builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(
		policy =>
		{
			policy.AllowAnyOrigin()
					.AllowAnyHeader()
					.AllowAnyMethod();
		});
});

// JWT Authentication Configuration
var keyBytes = Encoding.UTF8.GetBytes(builder.Configuration.GetSection("AppSettings:Token:Key").Value!);

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
builder.Services.AddSwaggerGen(config =>
{
	config.SwaggerDoc("v1", new OpenApiInfo()
	{
		Version = "v1",
		Title = "GeekCollection API",
		TermsOfService = new Uri("https://example.com/terms"),
		Description = "Api GeekCollection para geeks/nerds organizarem suas coleções.",
		Contact = new OpenApiContact()
		{
			Name = "Pedro Sawczuk",
			Email = "pedrojosawczuk@gmail.com",
			Url = new Uri("https://www.linkedin.com/in/swczk/")
		},
	});
	config.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
	{
		Description = "JWT Authorization header usando o esquema Bearer. \r\n\r\n" +
				"Digite 'Bearer' [espaço] e então seu token na entrada de texto abaixo.\r\n\r\n" +
				"Exemplo: 'Bearer 12345abcdef'",
		Name = "Authorization",
		In = ParameterLocation.Header,
		Type = SecuritySchemeType.ApiKey,
		Scheme = "Bearer"
	});
	config.AddSecurityRequirement(new OpenApiSecurityRequirement()
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

// Configurar o JSON Serializer para lidar com ciclos de referência
builder.Services.AddControllers().AddJsonOptions(options =>
{
	options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI(Style.Dark);
}

app.UseCors();

app.UseSerilogRequestLogging();

app.UseRouting();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/user/login", async (LoginModel userLogin, DBContext db) =>
{
	var user = await db.Users.FirstOrDefaultAsync(u => u.Email == userLogin.Email);

	if (user == null || !BCrypt.Net.BCrypt.Verify(userLogin.Password, user.Password))
	{
		return Results.Unauthorized();
	}
	string tokenExpiryString = builder.Configuration.GetSection("AppSettings:Token:Expires").Value!;
	bool isParsed = double.TryParse(tokenExpiryString, out double tokenExpiryHours);
	double expiryInHours = isParsed ? tokenExpiryHours : 24;

	var tokenHandler = new JwtSecurityTokenHandler();
	var tokenDescriptor = new SecurityTokenDescriptor
	{
		Subject = new ClaimsIdentity(new[]
		{
			new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
			new Claim(ClaimTypes.Email, user.Email)
		}),
		Expires = DateTime.UtcNow.AddHours(expiryInHours),
		SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
	};

	var token = tokenHandler.CreateToken(tokenDescriptor);
	var tokenString = tokenHandler.WriteToken(token);

	return Results.Ok(new { Token = tokenString });
})
.WithName("Login")
.WithOpenApi();

app.MapPost("/user/register", async (RegisterModel userRegister, DBContext db) =>
{
	var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == userRegister.Email);

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

app.MapGet("/user/verify", [Authorize] () =>
{
	return Results.Ok();
})
.WithName("VerifyToken")
.WithOpenApi();

app.MapPut("/user/update", [Authorize] async ([FromBody] UpdateProfileDto updatedProfile, DBContext db, HttpContext httpContext) =>
{
	if (!int.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
	{
		return Results.Unauthorized();
	}

	var existingUser = await db.Users.FindAsync(userId);

	if (existingUser == null)
	{
		return Results.NotFound("Usuário não encontrado.");
	}

	existingUser.Username = updatedProfile.Username ?? existingUser.Username;
	existingUser.Email = updatedProfile.Email ?? existingUser.Email;
	existingUser.Password = !string.IsNullOrEmpty(updatedProfile.Password)
		? BCrypt.Net.BCrypt.HashPassword(updatedProfile.Password)
		: existingUser.Password;
	existingUser.ProfilePicture = !string.IsNullOrEmpty(updatedProfile.ProfilePicture)
		? updatedProfile.ProfilePicture
		: existingUser.ProfilePicture;

	db.Users.Update(existingUser);
	await db.SaveChangesAsync();

	return Results.Ok("Perfil atualizado com sucesso.");
})
.WithName("UpdateProfile")
.WithOpenApi();

app.MapGet("/user/profile", [Authorize] async (DBContext db, HttpContext httpContext) =>
{
	if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value, out var userId))
	{
		return Results.Unauthorized();
	}

	var user = await db.Users
		 .Where(u => u.Id == userId)
		 .Select(u => new
		 {
			 u.Id,
			 u.Username,
			 u.Email,
			 u.ProfilePicture
		 })
		 .FirstOrDefaultAsync();

	if (user == null)
	{
		return Results.NotFound();
	}

	return Results.Ok(user);
})
.WithName("GetUser")
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
		 .Select(c => new
		 {
			 c.Id,
			 c.Name,
			 c.Description,
			 User = new
			 {
				 Id = c.User.Id,
				 Username = c.User.Username,
				 Email = c.User.Email,
				 ProfilePicture = c.User.ProfilePicture,
			 },
			 Shares = c.Shares.Select(s => new
			 {
				 s.Id,
				 s.SharedWithUserId,
				 User = new
				 {
					 Id = s.SharedWithUser.Id,
					 Username = s.SharedWithUser.Username,
					 Email = s.SharedWithUser.Email,
					 ProfilePicture = s.SharedWithUser.ProfilePicture,
				 }
			 }).ToList(),
			 Items = c.Items.Select(i => new ItemDto
			 {
				 Id = i.Id,
				 Name = i.Name,
				 CategoryId = i.CategoryId,
				 Description = i.Description,
				 Condition = i.Condition,
				 Category = new CategoryDto
				 {
					 Id = i.Category.Id,
					 Name = i.Category.Name
				 }
			 }).ToList()
		 })
		 .ToListAsync();

	return Results.Ok(collections);
})
.WithName("GetAllCollections")
.WithOpenApi();

app.MapGet("/collections/{id}", [Authorize] async (int id, DBContext db, HttpContext httpContext) =>
{
	if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value, out var userId))
	{
		return Results.Unauthorized();
	}

	var collection = await db.Collections
		.Include(c => c.Items)
		.ThenInclude(i => i.Category)
		.Include(c => c.Shares)
		.ThenInclude(s => s.SharedWithUser)
		.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

	if (collection == null)
	{
		return Results.NotFound();
	}

	var collectionDto = new CollectionDto
	{
		Id = collection.Id,
		Name = collection.Name,
		Description = collection.Description,
		UserId = collection.UserId,
		Shares = collection.Shares.Select(s => new ShareDto
		{
			Id = s.Id,
			User = new UserDto
			{
				Id = s.SharedWithUser.Id,
				Username = s.SharedWithUser.Username,
				Email = s.SharedWithUser.Email
			}
		}).ToList(),
		Items = collection.Items.Select(i => new ItemDto
		{
			Id = i.Id,
			Name = i.Name,
			Description = i.Description,
			Condition = i.Condition,
			Category = new CategoryDto
			{
				Id = i.Category.Id,
				Name = i.Category.Name
			}
		}).ToList()
	};

	return Results.Ok(collectionDto);
})
.WithName("GetCollectionById")
.WithOpenApi();

app.MapPost("/collections", [Authorize] async (CollectionCreateDto createModel, DBContext db, HttpContext httpContext) =>
{
	if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value, out var userId))
	{
		return Results.Unauthorized();
	}

	var collection = new Collection
	{
		Name = createModel.Name,
		Description = createModel.Description,
		UserId = userId
	};

	db.Collections.Add(collection);
	await db.SaveChangesAsync();

	return Results.Created($"/collections/{collection.Id}", collection);
})
.WithName("CreateCollection")
.WithOpenApi();

app.MapPut("/collections/{id}", [Authorize] async (int id, CollectionUpdateDto updatedCollection, DBContext db, HttpContext httpContext) =>
{
	if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value, out var userId))
	{
		return Results.Unauthorized();
	}

	var collection = await db.Collections
		.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

	if (collection == null)
	{
		return Results.NotFound("Coleção não encontrada ou você não tem permissão para editá-la.");
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

app.MapPost("/collections/{collectionId}/items", [Authorize] async (int collectionId, ItemCreateDto createModel, DBContext db, HttpContext httpContext) =>
{
	if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value, out var userId))
	{
		return Results.Unauthorized();
	}

	var collection = await db.Collections
		.FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId);

	if (collection == null)
	{
		return Results.NotFound("Coleção não encontrada.");
	}

	var item = new Item
	{
		Name = createModel.Name,
		CategoryId = createModel.CategoryId,
		Description = createModel.Description,
		Condition = createModel.Condition,
		CollectionId = collectionId
	};

	db.Items.Add(item);
	await db.SaveChangesAsync();

	var itemDto = new ItemDto
	{
		Id = item.Id,
		Name = item.Name,
		CategoryId = item.CategoryId,
		Description = item.Description,
		Condition = item.Condition
	};

	return Results.Created($"/collections/{collectionId}/items/{item.Id}", itemDto);
})
.WithName("CreateItem")
.WithOpenApi();

app.MapPut("/collections/{collectionId}/items/{itemId}", [Authorize] async (int collectionId, int itemId, ItemUpdateDto updatedItem, DBContext db, HttpContext httpContext) =>
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
	var categoryExists = await db.Categories.FirstOrDefaultAsync(c => c.Id == item.CategoryId);
	if (categoryExists?.Id == 0)
	{
		return Results.BadRequest();
	}

	item.Name = updatedItem.Name;
	item.CategoryId = updatedItem.CategoryId;
	item.Description = updatedItem.Description;
	item.Condition = updatedItem.Condition;

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

app.MapGet("/categories", async (DBContext db) =>
{
	var categories = await db.Categories
		.Select(c => new
		{
			Id = c.Id,
			Name = c.Name
		})
		.ToListAsync();

	return Results.Ok(categories);
})
.WithName("GetAllCategories")
.WithOpenApi();

app.MapGet("/collections/shares", [Authorize] async (DBContext db, HttpContext httpContext) =>
{
	if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)!.Value, out var userId))
	{
		return Results.Unauthorized();
	}

	var sharedCollections = await db.Shares
		.Where(s => s.SharedWithUserId == userId)
		.Select(s => s.Collection)
		 .Select(c => new
		 {
			 c.Id,
			 c.Name,
			 c.Description,
			 User = new
			 {
				 Id = c.User.Id,
				 Username = c.User.Username,
				 Email = c.User.Email,
				 ProfilePicture = c.User.ProfilePicture,
			 },
			 Shares = c.Shares.Select(s => new
			 {
				 s.Id,
				 s.SharedWithUserId,
				 User = new
				 {
					 Id = s.SharedWithUser.Id,
					 Username = s.SharedWithUser.Username,
					 Email = s.SharedWithUser.Email,
					 ProfilePicture = s.SharedWithUser.ProfilePicture,
				 }
			 }).ToList(),
			 Items = c.Items.Select(i => new ItemDto
			 {
				 Id = i.Id,
				 Name = i.Name,
				 CategoryId = i.CategoryId,
				 Description = i.Description,
				 Condition = i.Condition,
				 Category = new CategoryDto
				 {
					 Id = i.Category.Id,
					 Name = i.Category.Name
				 }
			 }).ToList()
		 })
		.ToListAsync();

	if (sharedCollections == null || !sharedCollections.Any())
	{
		return Results.NotFound("Nenhuma coleção compartilhada encontrada.");
	}

	return Results.Ok(sharedCollections);
})
.WithName("GetSharedCollections")
.WithOpenApi();

app.MapPost("/collections/{collectionId}/shares", [Authorize] async (int collectionId, [FromBody] string sharedWithEmail, DBContext db, HttpContext httpContext) =>
{
	if (!int.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
	{
		return Results.Unauthorized();
	}

	var collection = await db.Collections
		.Include(c => c.Shares)
		.FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId);

	if (collection == null)
	{
		return Results.NotFound("Coleção não encontrada ou você não tem permissão para compartilhá-la.");
	}

	var sharedWithUser = await db.Users
		.FirstOrDefaultAsync(u => u.Email == sharedWithEmail);

	if (sharedWithUser == null)
	{
		return Results.NotFound("Usuário para compartilhar não encontrado.");
	}

	var existingShare = collection.Shares
		.Any(s => s.SharedWithUserId == sharedWithUser.Id);

	if (existingShare)
	{
		return Results.Conflict("Coleção já compartilhada com o usuário especificado.");
	}

	var newShare = new Share
	{
		CollectionId = collectionId,
		SharedWithUserId = sharedWithUser.Id
	};

	db.Shares.Add(newShare);
	await db.SaveChangesAsync();

	return Results.Ok("Coleção compartilhada com sucesso.");
})
.WithName("ShareCollection")
.WithOpenApi();

app.MapDelete("/collections/{collectionId}/shares/{shareId}", [Authorize] async (int collectionId, int shareId, DBContext db, HttpContext httpContext) =>
{
	if (!int.TryParse(httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value, out var userId))
	{
		return Results.Unauthorized();
	}

	var share = await db.Shares
		.Include(s => s.Collection)
		.FirstOrDefaultAsync(s => s.Id == shareId);

	if (share == null)
	{
		return Results.NotFound("Compartilhamento não encontrado.");
	}

	if (share.Collection.UserId != userId && share.SharedWithUserId != userId)
	{
		return Results.Unauthorized();
	}

	db.Shares.Remove(share);
	await db.SaveChangesAsync();

	return Results.NoContent();
})
.WithName("DeleteShare")
.WithOpenApi();

app.Run();
