// Shared imports for the PostgreSQL project; the stores and configurations repeat these otherwise.
global using Microsoft.EntityFrameworkCore;
global using MuscleCuties.Infrastructure.PostgreSql.Persistence;
global using MuscleCuties.Infrastructure.PostgreSql.Models.App;
global using System.Text.Json;
global using MuscleCuties.Core.Domain.Workout.Planning;
global using MuscleCuties.Core.Models.Enums.Workout;
global using Npgsql;
global using MuscleCuties.UseCases.Features.Workout.Planning;
global using System.Security.Cryptography;
global using System.Globalization;
