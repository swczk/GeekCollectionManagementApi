<p align="center">
  <h1 align="center">Geek Collection Management Api</h1>
  <h3 align="center"></h3>
</p>

A backend API for managing geek collections, built with .NET 8.0. It provides features like user authentication with JWT, managing collections and shared items, and CRUD operations.

The project uses Docker and Docker Compose for containerization, making it easy to deploy and run the API along with a MariaDB database.

Check out the frontend of the application:
[Geek Collection App](https://github.com/swczk/geek_collection_app)

## How to Use?

### Database Setup
To initialize a MariaDB instance required by the backend, use the following Docker command:

```sh
docker-compose up
```
You can find the database schema in `db.sql`, which will be loaded into the database upon initialization.

### Running the Backend
To restore and build the backend project, follow these steps:

1. Restore the project dependencies:
```sh
dotnet restore src/Web/Web.csproj
```
2. Compile and run the backend:

```sh
dotnet run --project src/Web/Web.csproj
```

This will start the API server on the configured host and port.

## JSON Data Communication
### User Authorization via Token
The API uses token-based authorization to secure its endpoints. After logging in, the user must include the JWT token in the request headers to authenticate and authorize their actions. The token must be passed in the `Authorizatio` header of each request as a bearer token.

Here is an example of how to include the token in a request header after login:

```http
Authorization: Bearer <your-token-here>
```

## Example Endpoints

### Login
```http
POST {{API_URL}}/user/login
Content-Type: application/json
```

```json
{
  "email": "user@example.com",
  "password": "yourpassword"
}
```
### Register
```http
POST {{API_URL}}/user/register
Content-Type: application/json
```

```json
{
  "username": "newuser",
  "email": "newuser@example.com",
  "password": "yourpassword"
}
```

After successful registration or login, the API returns a JWT token, which must be used for subsequent authenticated requests.

## Additional Documentation
You can find more detailed documentation, including all available API endpoints and example requests, through the Swagger interface, accessible after running the backend.
