using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Domain.Nutrition;
using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Nutrition;
using MuscleCuties.Core.Models.Nutrition;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Nutrition.Planning;

namespace MuscleCuties.Core.Services.Nutrition;

public partial class NutritionTrackingService : INutritionTrackingService
{
    private const double BaselineCalories = 2000;
    private const double BaselineProtein = 130;
    private const double BaselineCarbs = 220;
    private const double BaselineFat = 65;
    private readonly IAuthService _authService;
    private readonly INutritionChangeNotifier? _changeNotifier;
    private readonly ICycleService? _cycleService;
    private readonly AppDatabase _db;
    private readonly INutritionPlanner? _nutritionPlanner;
    private readonly IUserRepository _userRepository;
    private readonly AccountSessionGate _sessionGate;

    public NutritionTrackingService(
        AppDatabase db,
        IAuthService authService,
        IUserRepository userRepository,
        INutritionPlanner? nutritionPlanner = null,
        ICycleService? cycleService = null,
        INutritionChangeNotifier? changeNotifier = null,
        AccountSessionGate? sessionGate = null)
    {
        _db = db;
        _authService = authService;
        _userRepository = userRepository;
        _nutritionPlanner = nutritionPlanner;
        _cycleService = cycleService;
        _changeNotifier = changeNotifier;
        _sessionGate = sessionGate ?? AccountSessionGate.Shared;
    }

    public async Task<DailyNutritionLogDto> GetLogForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var (userId, _, log) = await GetOrCreateDailyLogAsync(date, true, ct).ConfigureAwait(false);
        if (await UpdateTargetsAsync(log, userId, date).ConfigureAwait(false) ||
            _db.Entry(log).State == EntityState.Added)
            await SaveAsync(userId, ct).ConfigureAwait(false);
        var meals = await MealsAsync(userId, date, date, ct).ConfigureAwait(false);
        var entries = log.Entries.Where(e => !e.IsDeleted).Select(MapEntryToDto)
            .Concat(meals.Select(m => MapMeal(m, log.Id)))
            .OrderBy(e => e.MealType).ThenBy(e => e.LoggedAtTimestamp).ThenBy(e => e.Id).ToList();
        await EnsureCurrentUserAsync(userId).ConfigureAwait(false);
        return MapLogToDto(log, date, entries);
    }

    public async Task<FoodItemEntryDto?> GetFoodItemAsync(Guid id, CancellationToken ct = default)
    {
        var userId = await GetCurrentUserIdSafeAsync().ConfigureAwait(false);
        FoodItemEntryDto? result;
        if (CanonicalMealIdentity.TryExtractLoggedMealId(id, out var mealId))
        {
            var meal = await MealQuery(userId).FirstOrDefaultAsync(m => m.Id == mealId, ct).ConfigureAwait(false);
            result = meal is null ? null : MapMeal(meal, Guid.Empty);
        }
        else
        {
            var entry = await _db.FoodItemEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id &&
                    !e.IsDeleted && e.DailyLog != null && !e.DailyLog.IsDeleted && e.DailyLog.UserId == userId, ct)
                .ConfigureAwait(false);
            result = entry is null
                ? null
                : MapEntryToDto(entry) with
                {
                    LoggedAtTimestamp = DateOnly.ParseExact(entry.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                        .ToDateTime(TimeOnly.FromDateTime(entry.LoggedAtTimestamp))
                };
        }

        await EnsureCurrentUserAsync(userId).ConfigureAwait(false);
        return result;
    }

    public Task AddOrUpdateFoodItemAsync(DateOnly date, FoodItemEntryDto item, CancellationToken ct = default)
    {
        return WriteFoodAsync(date, item, false, ct);
    }

    public async Task AddFoodItemsBatchAsync(DateOnly date, IReadOnlyList<FoodItemEntryDto> items, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count < DailyFoodEntry.MinimumBatchItems) return;
        var entries = items.Select(item => DailyFoodEntry.Create(item.MealType, item.Name,
            item.ServingSize, item.ServingUnit, item.ServingsCount, item.Calories,
            item.Protein, item.Carbs, item.Fat)).ToList();
        DailyFoodEntry.ValidateBatch(entries);
        var sessionVersion = _sessionGate.Version;
        var userId = await GetCurrentUserIdSafeAsync().ConfigureAwait(false);
        await WriteBatchForOwnerAsync(date, items, userId, sessionVersion, ct).ConfigureAwait(false);
        Notify(userId, date, date);
    }

    public async Task<int> CopyEntriesAsync(DateOnly sourceDate, DateOnly targetDate,
        MealType? mealType, CancellationToken ct = default)
    {
        DailyFoodEntry.RequireDifferentDates(sourceDate, targetDate);
        if (mealType.HasValue) DailyFoodEntry.RequireMealType(mealType.Value);
        var sessionVersion = _sessionGate.Version;
        var userId = await GetCurrentUserIdSafeAsync().ConfigureAwait(false);
        var sourceDateText = sourceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var sourceLog = await _db.DailyNutritionLogs.AsNoTracking()
            .Include(log => log.Entries)
            .FirstOrDefaultAsync(log => log.UserId == userId && log.Date == sourceDateText && !log.IsDeleted, ct)
            .ConfigureAwait(false);
        var sourceMeals = await MealsAsync(userId, sourceDate, sourceDate, ct).ConfigureAwait(false);
        EnsureSessionVersion(sessionVersion);
        var selected = (sourceLog?.Entries.Where(item => !item.IsDeleted).Select(MapEntryToDto) ?? [])
            .Concat(sourceMeals.Select(meal => MapMeal(meal, sourceLog?.Id ?? Guid.Empty)))
            .Where(item => !mealType.HasValue || item.MealType == mealType.Value)
            .ToList();
        if (selected.Count < DailyFoodEntry.MinimumBatchItems) return 0;
        var copies = selected.Select(item => new FoodItemEntryDto(
            Guid.NewGuid(), Guid.Empty, item.MealType, item.Name, item.ServingSize,
            item.ServingUnit, item.ServingsCount, item.Calories, item.Protein,
            item.Carbs, item.Fat, DailyFoodEntry.CopyTimestamp(targetDate, item.LoggedAtTimestamp))
        {
            Ingredients = item.Ingredients,
            AcceptedSuggestionName = item.AcceptedSuggestionName,
            AcceptedSuggestionPhase = item.AcceptedSuggestionPhase
        }).ToList();
        DailyFoodEntry.ValidateBatch(copies.Select(item => DailyFoodEntry.Create(item.MealType,
            item.Name, item.ServingSize, item.ServingUnit, item.ServingsCount,
            item.Calories, item.Protein, item.Carbs, item.Fat)).ToList());
        await WriteBatchForOwnerAsync(targetDate, copies, userId, sessionVersion, ct).ConfigureAwait(false);
        Notify(userId, targetDate, targetDate);
        return copies.Count;
    }

    private async Task WriteBatchForOwnerAsync(DateOnly date, IReadOnlyList<FoodItemEntryDto> items,
        int userId, long sessionVersion, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            var dateStr = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var log = await _db.DailyNutritionLogs.Include(entry => entry.Entries)
                .FirstOrDefaultAsync(entry => entry.UserId == userId && entry.Date == dateStr && !entry.IsDeleted, ct)
                .ConfigureAwait(false) ?? await CreateDefaultLogEntityAsync(userId, date, ct).ConfigureAwait(false);
            foreach (var item in items)
            {
                var entry = new FoodItemEntry
                {
                    Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
                    DailyLogId = log.Id,
                    DailyLog = log,
                    Date = dateStr,
                    MealType = item.MealType,
                    Name = item.Name.Trim(),
                    ServingSize = item.ServingSize,
                    ServingUnit = item.ServingUnit.Trim(),
                    ServingsCount = item.ServingsCount,
                    Calories = item.Calories,
                    Protein = item.Protein,
                    Carbs = item.Carbs,
                    Fat = item.Fat,
                    LoggedAtTimestamp = DailyFoodEntry.CopyTimestamp(date, item.LoggedAtTimestamp),
                    UpdatedAt = DateTime.UtcNow
                };
                log.Entries.Add(entry);
                await _db.FoodItemEntries.AddAsync(entry, ct).ConfigureAwait(false);
            }
            RecalculateLogTotals(log);
            using var sessionLease = await _sessionGate.EnterAsync(ct).ConfigureAwait(false);
            EnsureSessionVersion(sessionVersion);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    private void EnsureSessionVersion(long version)
    {
        if (_sessionGate.Version != version)
            throw new InvalidOperationException("The signed-in account changed. Reopen your food log.");
    }

    public Task UpdateFoodItemAsync(DateOnly date, FoodItemEntryDto item, CancellationToken ct = default)
    {
        return WriteFoodAsync(date, item, true, ct);
    }

    public async Task DeleteFoodItemAsync(Guid itemId, CancellationToken ct = default)
    {
        var userId = await GetCurrentUserIdSafeAsync().ConfigureAwait(false);
        DateOnly date;
        try
        {
            if (CanonicalMealIdentity.TryExtractLoggedMealId(itemId, out var mealId))
            {
                var meal = await _db.LoggedMeals.FirstOrDefaultAsync(m => m.Id == mealId && m.UserId == userId, ct)
                    .ConfigureAwait(false) ?? throw Missing();
                date = MealDate(meal);
                _db.LoggedMeals.Remove(meal);
            }
            else
            {
                var entry = await _db.FoodItemEntries.Include(e => e.DailyLog).ThenInclude(l => l!.Entries)
                    .FirstOrDefaultAsync(e => e.Id == itemId && !e.IsDeleted && e.DailyLog != null &&
                                              !e.DailyLog.IsDeleted && e.DailyLog.UserId == userId, ct)
                    .ConfigureAwait(false) ?? throw Missing();
                date = DateOnly.ParseExact(entry.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                entry.IsDeleted = true;
                entry.DeletedAt = entry.UpdatedAt = DateTime.UtcNow;
                RecalculateLogTotals(entry.DailyLog!);
            }

            await SaveAsync(userId, ct).ConfigureAwait(false);
        }
        catch
        {
            _db.ChangeTracker.Clear();
            throw;
        }

        Notify(userId, date, date);
    }

    public async Task ReLogFoodAsync(DateOnly date, MealType mealType, QuickFoodItemDto item,
        FoodItemEntryDto draft, CancellationToken ct = default)
    {
        if (draft.MealType != mealType) throw new ArgumentException("Meal category changed.", nameof(draft));
        ValidateFood(draft);
        if (item.SourceId is not Guid sourceId)
        {
            // Catalog/manual foods carry no acceptance provenance.
            var newDraft = draft.Id == Guid.Empty ? draft with { Id = Guid.NewGuid() } : draft;
            await AddOrUpdateFoodItemAsync(date, newDraft, ct).ConfigureAwait(false);
            return;
        }

        var sessionVersion = _sessionGate.Version;
        var userId = await GetCurrentUserIdSafeAsync().ConfigureAwait(false);
        var source = await GetFoodItemAsync(sourceId, ct).ConfigureAwait(false) ?? throw Missing();
        EnsureSessionVersion(sessionVersion);
        if (!CanonicalMealIdentity.TryExtractLoggedMealId(sourceId, out _))
        {
            var newDraft = draft.Id == sourceId || draft.Id == Guid.Empty
                ? draft with { Id = Guid.NewGuid() }
                : draft;
            await WriteFoodAsync(date, newDraft, false, ct, sessionVersion).ConfigureAwait(false);
            return;
        }

        var ingredients = draft.Ingredients;
        if (ingredients.Count == 0) throw new InvalidOperationException("The saved portion is unavailable.");
        if (item.IngredientFoodItemId is int foodId &&
            (ingredients.Count != 1 || ingredients[0].FoodItemId != foodId ||
             !source.Ingredients.Any(i => i.FoodItemId == foodId)))
            throw Missing();
        var meal = new LoggedMeal
        {
            UserId = userId, Date = date.ToDateTime(TimeOnly.MinValue),
            LoggedAt = date.ToDateTime(TimeOnly.FromDateTime(draft.LoggedAtTimestamp)),
            CreatedAt = DateTime.UtcNow, MealType = mealType,
            AcceptedSuggestionName = item.IngredientFoodItemId is null ? source.AcceptedSuggestionName : null,
            AcceptedSuggestionPhase = item.IngredientFoodItemId is null ? source.AcceptedSuggestionPhase : null
        };
        try
        {
            await ReplaceIngredientsAsync(meal, ingredients, ct).ConfigureAwait(false);
            await _db.LoggedMeals.AddAsync(meal, ct).ConfigureAwait(false);
            await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            using var sessionLease = await _sessionGate.EnterAsync(ct).ConfigureAwait(false);
            EnsureSessionVersion(sessionVersion);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            _db.ChangeTracker.Clear();
            throw;
        }

        Notify(userId, date, date);
    }

    public async Task<IReadOnlyList<DailyNutritionSummaryDto>> GetDateRangeSummariesAsync(DateOnly start, DateOnly end,
        CancellationToken ct = default)
    {
        if (end < start) throw new ArgumentOutOfRangeException(nameof(end));
        var userId = await GetCurrentUserIdSafeAsync().ConfigureAwait(false);
        var startStr = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var endStr = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
#pragma warning disable CA1309 // SQLite translates ISO date comparisons.
        var logs = await _db.DailyNutritionLogs.AsNoTracking().Include(l => l.Entries.Where(e => !e.IsDeleted))
            .Where(l => l.UserId == userId && !l.IsDeleted && string.Compare(l.Date, startStr) >= 0 &&
                        string.Compare(l.Date, endStr) <= 0)
            .ToListAsync(ct).ConfigureAwait(false);
#pragma warning restore CA1309
        var meals = await MealsAsync(userId, start, end, ct).ConfigureAwait(false);
        var byDate = meals.ToLookup(MealDate);
        var logsByDate =
            logs.ToDictionary(l => DateOnly.ParseExact(l.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        var result = logsByDate.Keys.Concat(byDate.Select(g => g.Key)).Distinct().Order()
            .Select(date =>
            {
                logsByDate.TryGetValue(date, out var log);
                var entries = (log?.Entries.Select(MapEntryToDto) ?? [])
                    .Concat(byDate[date].Select(m => MapMeal(m, log?.Id ?? Guid.Empty))).ToList();
                return new DailyNutritionSummaryDto(date, log?.Id ?? Guid.Empty,
                    Math.Round(entries.Sum(e => e.Calories), 1), Math.Round(entries.Sum(e => e.Protein), 1),
                    Math.Round(entries.Sum(e => e.Carbs), 1), Math.Round(entries.Sum(e => e.Fat), 1),
                    log?.TargetCalories ?? BaselineCalories, log?.WaterIntakeMl ?? 0, entries.Count);
            }).ToList();
        await EnsureCurrentUserAsync(userId).ConfigureAwait(false);
        return result;
    }

    public async Task UpdateWaterIntakeAsync(DateOnly date, int waterIntakeMl, CancellationToken ct = default)
    {
        var (userId, _, log) = await GetOrCreateDailyLogAsync(date, false, ct).ConfigureAwait(false);
        log.WaterIntakeMl = Math.Max(0, waterIntakeMl);
        log.UpdatedAt = DateTime.UtcNow;
        await SaveAsync(userId, ct).ConfigureAwait(false);
        Notify(userId, date, date);
    }

    public async Task UpdateNotesAsync(DateOnly date, string? notes, CancellationToken ct = default)
    {
        var (userId, _, log) = await GetOrCreateDailyLogAsync(date, false, ct).ConfigureAwait(false);
        log.Notes = notes;
        log.UpdatedAt = DateTime.UtcNow;
        await SaveAsync(userId, ct).ConfigureAwait(false);
        Notify(userId, date, date);
    }

    private async Task WriteFoodAsync(DateOnly date, FoodItemEntryDto item, bool requireExisting,
        CancellationToken ct, long? expectedSessionVersion = null)
    {
        ValidateFood(item);
        DailyFoodEntry.Create(item.MealType, item.Name, item.ServingSize, item.ServingUnit,
            item.ServingsCount, item.Calories, item.Protein, item.Carbs, item.Fat);
        var sessionVersion = expectedSessionVersion ?? _sessionGate.Version;
        var userId = await GetCurrentUserIdSafeAsync().ConfigureAwait(false);
        var timestamp =
            date.ToDateTime(
                TimeOnly.FromDateTime(item.LoggedAtTimestamp == default ? DateTime.Now : item.LoggedAtTimestamp));
        var oldDate = date;
        try
        {
            if (CanonicalMealIdentity.TryExtractLoggedMealId(item.Id, out var mealId))
                oldDate = await WriteLoggedMealAsync(userId, date, item, mealId, ct).ConfigureAwait(false);
            else
                oldDate = await WriteFoodItemEntryAsync(userId, date, item, requireExisting, timestamp, ct)
                    .ConfigureAwait(false);
            await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            using var sessionLease = await _sessionGate.EnterAsync(ct).ConfigureAwait(false);
            EnsureSessionVersion(sessionVersion);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            _db.ChangeTracker.Clear();
            throw;
        }

        Notify(userId, date, oldDate);
    }

    private async Task<DateOnly> WriteLoggedMealAsync(int userId, DateOnly date, FoodItemEntryDto item, int mealId,
        CancellationToken ct)
    {
        var meal = await _db.LoggedMeals.Include(m => m.Entries)
                       .FirstOrDefaultAsync(m => m.Id == mealId && m.UserId == userId, ct).ConfigureAwait(false)
                   ?? throw Missing();
        var oldDate = MealDate(meal);
        if (item.Ingredients.Count == 0)
        {
            if (meal.Entries.Count > 1)
                throw new ArgumentException("A grouped meal requires ingredients.", nameof(item));

            var customFood = new FoodItem
            {
                Name = item.Name.Trim(), Calories = (float)item.Calories,
                Protein = (float)item.Protein, Carbs = (float)item.Carbs,
                Fats = (float)item.Fat, ServingSize = 100,
                ServingSizeUnit = "g", IsCustom = true, DataType = "Custom",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            _db.LoggedMealEntries.RemoveRange(meal.Entries);
            meal.Entries.Clear();
            meal.Entries.Add(new LoggedMealEntry { FoodItem = customFood, Grams = 100 });
            meal.AcceptedSuggestionName = null;
            meal.AcceptedSuggestionPhase = null;
        }
        else
        {
            await ReplaceIngredientsAsync(meal, item.Ingredients, ct).ConfigureAwait(false);
        }

        meal.MealType = item.MealType;
        meal.Date = date.ToDateTime(TimeOnly.MinValue);
        var existingTime = TimeOnly.FromDateTime(meal.LoggedAt != default ? meal.LoggedAt :
            item.LoggedAtTimestamp != default ? item.LoggedAtTimestamp : meal.Date);
        meal.LoggedAt = date.ToDateTime(existingTime);
        return oldDate;
    }

    private async Task<DateOnly> WriteFoodItemEntryAsync(
        int userId,
        DateOnly date,
        FoodItemEntryDto item,
        bool requireExisting,
        DateTime timestamp,
        CancellationToken ct)
    {
        var oldDate = date;
        var existing = await _db.FoodItemEntries.Include(e => e.DailyLog).ThenInclude(l => l!.Entries)
            .FirstOrDefaultAsync(e => e.Id == item.Id, ct).ConfigureAwait(false);
        if (existing is not null && (existing.IsDeleted || existing.DailyLog is null ||
                                     existing.DailyLog.IsDeleted || existing.DailyLog.UserId != userId))
            throw Missing();
        if (requireExisting && existing is null) throw Missing();
        var (_, dateStr, log) = await GetOrCreateDailyLogAsync(date, true, ct).ConfigureAwait(false);
        var previousLog = existing?.DailyLog;
        if (previousLog is not null)
        {
            oldDate = DateOnly.ParseExact(previousLog.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (previousLog.Id != log.Id) previousLog.Entries.Remove(existing!);
        }

        var entry = existing ?? new FoodItemEntry { Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id };
        entry.DailyLogId = log.Id;
        entry.DailyLog = log;
        entry.Date = dateStr;
        entry.MealType = item.MealType;
        entry.Name = item.Name.Trim();
        entry.ServingSize = item.ServingSize;
        entry.ServingUnit = item.ServingUnit.Trim();
        entry.ServingsCount = item.ServingsCount;
        entry.Calories = item.Calories;
        entry.Protein = item.Protein;
        entry.Carbs = item.Carbs;
        entry.Fat = item.Fat;
        entry.LoggedAtTimestamp = timestamp;
        entry.UpdatedAt = DateTime.UtcNow;
        if (!log.Entries.Contains(entry)) log.Entries.Add(entry);
        if (existing is null) await _db.FoodItemEntries.AddAsync(entry, ct).ConfigureAwait(false);
        RecalculateLogTotals(log);
        if (previousLog is not null && previousLog.Id != log.Id) RecalculateLogTotals(previousLog);
        return oldDate;
    }

    private async Task ReplaceIngredientsAsync(LoggedMeal meal, IReadOnlyList<HistoryIngredientDto> ingredients,
        CancellationToken ct)
    {
        if (ingredients.Count == 0 ||
            ingredients.Any(i => i.FoodItemId <= 0 || !float.IsFinite(i.Grams) || i.Grams <= 0))
            throw new ArgumentException("Each ingredient needs a valid portion in grams.", nameof(ingredients));
        var ids = ingredients.Select(i => i.FoodItemId).Distinct().ToArray();
        if (await _db.FoodItems.CountAsync(f => ids.Contains(f.Id), ct).ConfigureAwait(false) != ids.Length)
            throw new InvalidOperationException("An ingredient is no longer available.");
        _db.LoggedMealEntries.RemoveRange(meal.Entries);
        meal.Entries.Clear();
        foreach (var ingredient in ingredients)
            meal.Entries.Add(new LoggedMealEntry { FoodItemId = ingredient.FoodItemId, Grams = ingredient.Grams });
    }

    private async Task SaveAsync(int userId, CancellationToken ct)
    {
        await EnsureCurrentUserAsync(userId).ConfigureAwait(false);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task EnsureCurrentUserAsync(int userId)
    {
        if (await GetCurrentUserIdSafeAsync().ConfigureAwait(false) != userId)
            throw new InvalidOperationException("The signed-in account changed. Reopen your food log.");
    }

    private void Notify(int userId, DateOnly date, DateOnly oldDate)
    {
        _changeNotifier?.NotifyNutritionChanged(userId, date);
        if (oldDate != date) _changeNotifier?.NotifyNutritionChanged(userId, oldDate);
    }

    private static InvalidOperationException Missing()
    {
        return new InvalidOperationException("This entry is unavailable. Refresh your food log.");
    }

    private static void ValidateFood(FoodItemEntryDto item)
    {
        if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.ServingUnit) ||
            !Enum.IsDefined(item.MealType) ||
            !double.IsFinite(item.ServingSize) || item.ServingSize <= 0 || !double.IsFinite(item.ServingsCount) ||
            item.ServingsCount <= 0 ||
            new[] { item.Calories, item.Protein, item.Carbs, item.Fat }.Any(v => !double.IsFinite(v) || v < 0))
            throw new ArgumentException("Enter a name, positive portion and finite, non-negative nutrition values.",
                nameof(item));
    }

    private static void RecalculateLogTotals(DailyNutritionLog log)
    {
        var entries = log.Entries.Where(e => !e.IsDeleted).ToList();
        DailyFoodEntry.ValidateBatch(entries.Select(entry => DailyFoodEntry.Create(entry.MealType,
            entry.Name, entry.ServingSize, entry.ServingUnit, entry.ServingsCount,
            entry.Calories, entry.Protein, entry.Carbs, entry.Fat)).ToList());
        log.TotalCalories = Math.Round(entries.Sum(e => e.Calories), 1);
        log.TotalProtein = Math.Round(entries.Sum(e => e.Protein), 1);
        log.TotalCarbs = Math.Round(entries.Sum(e => e.Carbs), 1);
        log.TotalFat = Math.Round(entries.Sum(e => e.Fat), 1);
        log.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<int> GetCurrentUserIdSafeAsync()
    {
        var id = await _authService.GetCurrentUserIdAsync().ConfigureAwait(false);
        return id > 0 ? id : throw new InvalidOperationException("Sign in before viewing food logs.");
    }

    private async Task<DailyNutritionLog> CreateDefaultLogEntityAsync(int userId, DateOnly date, CancellationToken ct)
    {
        var dateStr = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var now = DateTime.UtcNow;

        var targetCal = BaselineCalories;
        var targetP = BaselineProtein;
        var targetC = BaselineCarbs;
        var targetF = BaselineFat;

        try
        {
            var profile = await _userRepository.GetProfileAsync(userId).ConfigureAwait(false);
            if (_nutritionPlanner is not null && _cycleService is not null)
            {
                var phase = await _cycleService.GetPhaseForDateAsync(userId, date.ToDateTime(TimeOnly.MinValue))
                    .ConfigureAwait(false);
                var plan = profile is null
                    ? _nutritionPlanner.CreateFallbackPlan(phase)
                    : _nutritionPlanner.CreateDailyPlan(profile, phase, date.ToDateTime(TimeOnly.MinValue));
                targetCal = plan.Calories;
                targetP = plan.Protein;
                targetC = plan.Carbs;
                targetF = plan.Fats;
            }
            else if (profile is not null && float.IsFinite(profile.Weight) && profile.Weight is >= 30f and <= 500f)
            {
                targetP = Math.Round(profile.Weight * 1.8, 0);
                targetF = Math.Round(profile.Weight * 0.8, 0);
                targetCal = Math.Round(profile.Weight * 32.0, 0);
                var remainingCals = targetCal - (targetP * 4 + targetF * 9);
                targetC = Math.Max(50, Math.Round(remainingCals / 4.0, 0));
            }
        }
        catch
        {
            // Deterministic clinical fallback
        }

        var newLog = new DailyNutritionLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Date = dateStr,
            WaterIntakeMl = 0,
            TargetCalories = targetCal,
            TargetProtein = targetP,
            TargetCarbs = targetC,
            TargetFat = targetF,
            TotalCalories = 0,
            TotalProtein = 0,
            TotalCarbs = 0,
            TotalFat = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _db.DailyNutritionLogs.AddAsync(newLog, ct).ConfigureAwait(false);
        return newLog;
    }

    private async Task<bool> UpdateTargetsAsync(DailyNutritionLog log, int userId, DateOnly date)
    {
        if (_nutritionPlanner is null || _cycleService is null)
            return false;

        var profile = await _userRepository.GetProfileAsync(userId).ConfigureAwait(false);
        var phase = await _cycleService.GetPhaseForDateAsync(userId, date.ToDateTime(TimeOnly.MinValue))
            .ConfigureAwait(false);
        var plan = profile is null
            ? _nutritionPlanner.CreateFallbackPlan(phase)
            : _nutritionPlanner.CreateDailyPlan(profile, phase, date.ToDateTime(TimeOnly.MinValue));
        if (log.TargetCalories == plan.Calories && log.TargetProtein == plan.Protein &&
            log.TargetCarbs == plan.Carbs && log.TargetFat == plan.Fats)
            return false;

        log.TargetCalories = plan.Calories;
        log.TargetProtein = plan.Protein;
        log.TargetCarbs = plan.Carbs;
        log.TargetFat = plan.Fats;
        log.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    private static FoodItemEntryDto MapEntryToDto(FoodItemEntry entry)
    {
        return new FoodItemEntryDto(
            entry.Id,
            entry.DailyLogId,
            entry.MealType,
            entry.Name,
            entry.ServingSize,
            entry.ServingUnit,
            entry.ServingsCount,
            entry.Calories,
            entry.Protein,
            entry.Carbs,
            entry.Fat,
            entry.LoggedAtTimestamp
        );
    }

    private static DailyNutritionLogDto MapLogToDto(
        DailyNutritionLog log,
        DateOnly date,
        IReadOnlyList<FoodItemEntryDto> entries)
    {
        return new DailyNutritionLogDto(
            log.Id,
            date,
            log.WaterIntakeMl,
            log.TargetCalories,
            log.TargetProtein,
            log.TargetCarbs,
            log.TargetFat,
            Math.Round(entries.Sum(e => e.Calories), 1),
            Math.Round(entries.Sum(e => e.Protein), 1),
            Math.Round(entries.Sum(e => e.Carbs), 1),
            Math.Round(entries.Sum(e => e.Fat), 1),
            log.Notes,
            log.CreatedAt,
            log.UpdatedAt,
            entries
        );
    }

    private async Task<(int UserId, string DateStr, DailyNutritionLog Log)> GetOrCreateDailyLogAsync(DateOnly date,
        bool includeEntries, CancellationToken ct)
    {
        var userId = await GetCurrentUserIdSafeAsync().ConfigureAwait(false);
        var dateStr = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var query = _db.DailyNutritionLogs.AsQueryable();
        if (includeEntries)
            query = query.Include(l => l.Entries);

        var log = await query
            .FirstOrDefaultAsync(l => l.UserId == userId && l.Date == dateStr && !l.IsDeleted, ct)
            .ConfigureAwait(false);

        if (log is null) log = await CreateDefaultLogEntityAsync(userId, date, ct).ConfigureAwait(false);

        return (userId, dateStr, log);
    }
}
