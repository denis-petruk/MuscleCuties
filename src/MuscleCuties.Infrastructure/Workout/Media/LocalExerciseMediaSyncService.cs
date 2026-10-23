using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.ExerciseMedia;

namespace MuscleCuties.Infrastructure.Workout.Media;

/// <summary>
///     Offline-first synchronization service for exercise taxonomy and vector/lottie media.
///     Downloads external media asynchronously, caches to local storage, and enforces that
///     Exercise entity ImageUrl and VideoUrl store strictly local paths or file:// URIs.
/// </summary>
public sealed class LocalExerciseMediaSyncService : IExerciseMediaSyncService
{
    private const int MaxDownloadRetries = 2;
    private static readonly TimeSpan DefaultRateLimitDelay = TimeSpan.FromMilliseconds(50);

    private readonly AppDatabase _database;
    private readonly IExerciseDbClient _exerciseDbClient;
    private readonly IExerciseMediaStore _mediaStore;
    private readonly IWgerApiClient _wgerClient;

    public LocalExerciseMediaSyncService(
        AppDatabase database,
        IWgerApiClient wgerClient,
        IExerciseDbClient exerciseDbClient,
        IExerciseMediaStore mediaStore)
    {
        _database = database;
        _wgerClient = wgerClient;
        _exerciseDbClient = exerciseDbClient;
        _mediaStore = mediaStore;
    }

    public async Task<ExerciseSyncResult> SyncExerciseCatalogAsync(CancellationToken ct = default)
    {
        var result = new ExerciseSyncResult();

        try
        {
            // 1. Sync anatomical vector SVGs from Wger
            var muscleSvgs = await _wgerClient.GetMuscleSvgsAsync(ct).ConfigureAwait(false);
            var muscleSvgPaths = await CacheMuscleSvgsAsync(muscleSvgs, ct).ConfigureAwait(false);
            result.TotalMediaDownloaded += muscleSvgPaths.Count;

            // 2. Load existing taxonomy cache
            var existingExercises = await _database.Exercises.ToListAsync(ct).ConfigureAwait(false);
            var existingByCode = existingExercises
                .Where(e => !string.IsNullOrWhiteSpace(e.Code))
                .ToDictionary(e => e.Code, StringComparer.OrdinalIgnoreCase);
            var existingByName = existingExercises
                .GroupBy(e => Normalize(e.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var toAdd = new List<Exercise>();

            // 3. Sync Wger relational exercises
            await SyncWgerExercisesAsync(existingByCode, existingByName, muscleSvgPaths, toAdd, result, ct)
                .ConfigureAwait(false);

            // 4. Sync ExerciseDB taxonomy (bypassing GIFs with local Lottie animation mapping)
            await SyncExerciseDbExercisesAsync(existingByCode, existingByName, muscleSvgPaths, toAdd, result, ct)
                .ConfigureAwait(false);

            if (toAdd.Count > 0)
                await _database.Exercises.AddRangeAsync(toAdd, ct).ConfigureAwait(false);

            await _database.SaveChangesAsync(ct).ConfigureAwait(false);
            result.IsSuccess = true;
            result.Message =
                $"Catalog synced. Ingested: {result.TotalIngested}, Updated: {result.TotalUpdated}, Media: {result.TotalMediaDownloaded}";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            result.IsSuccess = false;
            result.Message = "Sync operation was cancelled.";
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[{nameof(LocalExerciseMediaSyncService)}] Catalog sync failed ({ex.GetType().Name}).");
            result.IsSuccess = false;
            result.Message = $"Catalog sync failed ({ex.GetType().Name}).";
        }

        return result;
    }

    public async Task<ExerciseMediaDownloadResult> DownloadMissingMediaAsync(CancellationToken ct = default)
    {
        var result = new ExerciseMediaDownloadResult();

        try
        {
            var exercises = await _database.Exercises.ToListAsync(ct).ConfigureAwait(false);
            var updated = false;

            // Cache anatomical muscle SVGs
            var muscleSvgs = await _wgerClient.GetMuscleSvgsAsync(ct).ConfigureAwait(false);
            var muscleSvgPaths = await CacheMuscleSvgsAsync(muscleSvgs, ct).ConfigureAwait(false);
            result.VectorSvgsDownloaded += muscleSvgPaths.Count;

            foreach (var exercise in exercises)
            {
                if (ct.IsCancellationRequested)
                    break;

                // Fix non-local or missing ImageUrl
                if (string.IsNullOrWhiteSpace(exercise.ImageUrl) || !ExerciseMediaPaths.IsLocalMedia(exercise.ImageUrl))
                {
                    if (muscleSvgPaths.TryGetValue(exercise.PrimaryMuscle, out var localSvg))
                    {
                        exercise.ImageUrl = localSvg;
                        updated = true;
                    }
                    else
                    {
                        exercise.ImageUrl = null;
                    }
                }

                // Fix non-local VideoUrl (never allow http / gifs)
                if (exercise.VideoUrl is not null && !ExerciseMediaPaths.IsLocalMedia(exercise.VideoUrl))
                {
                    exercise.VideoUrl = null;
                    updated = true;
                }
            }

            if (updated)
                await _database.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.WriteLine(
                $"[{nameof(LocalExerciseMediaSyncService)}] Missing media download failed ({ex.GetType().Name}).");
            result.FailedCount++;
        }

        return result;
    }

    public async Task<string?> EnsureExerciseVisualMediaAsync(int exerciseId, CancellationToken ct = default)
    {
        if (exerciseId <= 0)
            return null;

        var exercise = await _database.Exercises.FirstOrDefaultAsync(e => e.Id == exerciseId, ct).ConfigureAwait(false);
        if (exercise is null)
            return null;

        if (!string.IsNullOrWhiteSpace(exercise.ImageUrl) && ExerciseMediaPaths.IsLocalMedia(exercise.ImageUrl))
            return exercise.ImageUrl;

        // Try muscle SVG fallback
        var muscleKey = exercise.PrimaryMuscle.ToString().ToLowerInvariant();
        var localPath = _mediaStore.GetLocalFilePath(ExerciseMediaPaths.SvgFolder, $"{muscleKey}.svg");
        if (File.Exists(localPath))
        {
            exercise.ImageUrl = ExerciseMediaPaths.ToLocalFileUri(localPath);
            await _database.SaveChangesAsync(ct).ConfigureAwait(false);
            return exercise.ImageUrl;
        }

        return null;
    }

    private async Task SyncWgerExercisesAsync(
        Dictionary<string, Exercise> existingByCode,
        Dictionary<string, Exercise> existingByName,
        Dictionary<MuscleGroup, string> muscleSvgPaths,
        List<Exercise> toAdd,
        ExerciseSyncResult result,
        CancellationToken ct)
    {
        var wgerExercises = await _wgerClient.GetExercisesAsync(50, 0, ct).ConfigureAwait(false);
        foreach (var wgerItem in wgerExercises)
        {
            if (ct.IsCancellationRequested)
                break;

            var code = $"WGER_{wgerItem.Id}";
            var normalizedName = Normalize(wgerItem.Name);

            existingByCode.TryGetValue(code, out var existing);
            if (existing is null && existingByName.TryGetValue(normalizedName, out var match))
                existing = match;

            var primaryMuscle = MapWgerMuscle(wgerItem.Muscles.FirstOrDefault()?.Name);
            var secondaryNames = wgerItem.MusclesSecondary.Select(m => m.Name).Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            // Download vector SVG if available from image list
            string? localSvgPath = null;
            var svgImage =
                wgerItem.Images.FirstOrDefault(img =>
                    img.ImageUrl.EndsWith(".svg", StringComparison.OrdinalIgnoreCase));
            if (svgImage is not null && Uri.TryCreate(svgImage.ImageUrl, UriKind.Absolute, out var svgUri))
            {
                localSvgPath = await DownloadAndCacheSvgAsync(svgUri, $"wger_{wgerItem.Id}", ct).ConfigureAwait(false);
                if (localSvgPath is not null)
                    result.TotalMediaDownloaded++;
            }

            // Fall back to anatomical muscle SVG if exercise-specific SVG is absent
            if (string.IsNullOrWhiteSpace(localSvgPath))
                muscleSvgPaths.TryGetValue(primaryMuscle, out localSvgPath);

            if (existing is null)
            {
                var newExercise = new Exercise
                {
                    Code = code,
                    Name = wgerItem.Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(wgerItem.Description)
                        ? "Exercise movement."
                        : StripHtml(wgerItem.Description),
                    PrimaryMuscle = primaryMuscle,
                    SecondaryMuscles = string.Join(',', secondaryNames),
                    ImageUrl = localSvgPath,
                    VideoUrl = null,
                    IsInjuryFriendly = true
                };
                toAdd.Add(newExercise);
                existingByCode[code] = newExercise;
                result.TotalIngested++;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(localSvgPath) && !ExerciseMediaPaths.IsLocalMedia(existing.ImageUrl))
                {
                    existing.ImageUrl = localSvgPath;
                    result.TotalUpdated++;
                }
                else if (existing.ImageUrl is not null && !ExerciseMediaPaths.IsLocalMedia(existing.ImageUrl))
                {
                    // Clean legacy non-local URL to enforce offline-first local file caching
                    existing.ImageUrl = localSvgPath;
                    result.TotalUpdated++;
                }
            }

            await Task.Delay(DefaultRateLimitDelay, ct).ConfigureAwait(false);
        }
    }

    private async Task SyncExerciseDbExercisesAsync(
        Dictionary<string, Exercise> existingByCode,
        Dictionary<string, Exercise> existingByName,
        Dictionary<MuscleGroup, string> muscleSvgPaths,
        List<Exercise> toAdd,
        ExerciseSyncResult result,
        CancellationToken ct)
    {
        var exerciseDbItems = await _exerciseDbClient.GetExercisesAsync(50, 0, ct).ConfigureAwait(false);
        foreach (var edbItem in exerciseDbItems)
        {
            if (ct.IsCancellationRequested)
                break;

            var code = $"EDB_{edbItem.Id}";
            var normalizedName = Normalize(edbItem.Name);

            existingByCode.TryGetValue(code, out var existing);
            if (existing is null && existingByName.TryGetValue(normalizedName, out var match))
                existing = match;

            var primaryMuscle = MapExerciseDbTarget(edbItem.Target);
            muscleSvgPaths.TryGetValue(primaryMuscle, out var fallbackSvg);

            if (existing is null)
            {
                var newExercise = new Exercise
                {
                    Code = code,
                    Name = FormatTitle(edbItem.Name),
                    Description = edbItem.Instructions.Count > 0
                        ? string.Join(" ", edbItem.Instructions)
                        : $"A {edbItem.Target} exercise targeting {edbItem.BodyPart}.",
                    PrimaryMuscle = primaryMuscle,
                    SecondaryMuscles = string.Join(',', edbItem.SecondaryMuscles),
                    ImageUrl = fallbackSvg,
                    // Note: External gifUrl is bypassed per MAUI CollectionView constraint
                    VideoUrl = null,
                    IsInjuryFriendly = true
                };
                toAdd.Add(newExercise);
                existingByCode[code] = newExercise;
                result.TotalIngested++;
            }
            else
            {
                // Enforce that VideoUrl NEVER holds external GIF or HTTP URL
                if (existing.VideoUrl is not null && !ExerciseMediaPaths.IsLocalMedia(existing.VideoUrl))
                {
                    existing.VideoUrl = null;
                    result.TotalUpdated++;
                }

                if (string.IsNullOrWhiteSpace(existing.ImageUrl) && !string.IsNullOrWhiteSpace(fallbackSvg))
                {
                    existing.ImageUrl = fallbackSvg;
                    result.TotalUpdated++;
                }
            }

            await Task.Delay(DefaultRateLimitDelay, ct).ConfigureAwait(false);
        }
    }

    private async Task<Dictionary<MuscleGroup, string>> CacheMuscleSvgsAsync(
        IReadOnlyList<WgerMuscleSvgResult> muscleSvgs,
        CancellationToken ct)
    {
        var result = new Dictionary<MuscleGroup, string>();

        foreach (var item in muscleSvgs)
        {
            if (string.IsNullOrWhiteSpace(item.SvgUrl) || !Uri.TryCreate(item.SvgUrl, UriKind.Absolute, out var uri))
                continue;

            var muscle = MapWgerMuscle(item.Name);
            var fileName = $"muscle_{muscle.ToString().ToLowerInvariant()}";

            var cachedPath = _mediaStore.GetLocalFilePath(ExerciseMediaPaths.SvgFolder, $"{fileName}.svg");
            if (File.Exists(cachedPath))
            {
                result[muscle] = ExerciseMediaPaths.ToLocalFileUri(cachedPath);
                continue;
            }

            var localPath = await DownloadAndCacheSvgAsync(uri, fileName, ct).ConfigureAwait(false);
            if (localPath is not null)
                result[muscle] = ExerciseMediaPaths.ToLocalFileUri(localPath);

            await Task.Delay(DefaultRateLimitDelay, ct).ConfigureAwait(false);
        }

        return result;
    }

    private async Task<string?> DownloadAndCacheSvgAsync(Uri svgUri, string fileName, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= MaxDownloadRetries; attempt++)
            try
            {
                await using var stream = await _wgerClient.DownloadMediaStreamAsync(svgUri, ct).ConfigureAwait(false);
                if (stream is null)
                    return null;

                return await _mediaStore.SaveVectorSvgAsync(stream, fileName, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < MaxDownloadRetries)
            {
                Trace.WriteLine($"[{nameof(LocalExerciseMediaSyncService)}] Retry download ({ex.GetType().Name}).");
                await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[{nameof(LocalExerciseMediaSyncService)}] Failed download ({ex.GetType().Name}).");
                return null;
            }

        return null;
    }

    private static MuscleGroup MapWgerMuscle(string? muscleName)
    {
        if (string.IsNullOrWhiteSpace(muscleName))
            return MuscleGroup.Glutes;

        var name = muscleName.ToLowerInvariant();
        if (name.Contains("quad") || name.Contains("rectus femoris")) return MuscleGroup.Quads;
        if (name.Contains("glute")) return MuscleGroup.Glutes;
        if (name.Contains("hamstring") || name.Contains("biceps femoris")) return MuscleGroup.Hamstrings;
        if (name.Contains("chest") || name.Contains("pectoral")) return MuscleGroup.Chest;
        if (name.Contains("latissimus") || name.Contains("lat")) return MuscleGroup.Lats;
        if (name.Contains("biceps")) return MuscleGroup.Biceps;
        if (name.Contains("triceps")) return MuscleGroup.Triceps;
        if (name.Contains("shoulder") || name.Contains("deltoid")) return MuscleGroup.FrontShoulders;
        if (name.Contains("abs") || name.Contains("abdominal") || name.Contains("core")) return MuscleGroup.Abs;
        if (name.Contains("calf") || name.Contains("gastrocnemius")) return MuscleGroup.Calves;
        if (name.Contains("back")) return MuscleGroup.UpperBack;

        return MuscleGroup.Glutes;
    }

    private static MuscleGroup MapExerciseDbTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return MuscleGroup.Glutes;

        var t = target.ToLowerInvariant();
        if (t.Contains("quad")) return MuscleGroup.Quads;
        if (t.Contains("glute")) return MuscleGroup.Glutes;
        if (t.Contains("hamstring")) return MuscleGroup.Hamstrings;
        if (t.Contains("pectorals") || t.Contains("chest")) return MuscleGroup.Chest;
        if (t.Contains("lats")) return MuscleGroup.Lats;
        if (t.Contains("biceps")) return MuscleGroup.Biceps;
        if (t.Contains("triceps")) return MuscleGroup.Triceps;
        if (t.Contains("delts") || t.Contains("shoulders")) return MuscleGroup.FrontShoulders;
        if (t.Contains("abs") || t.Contains("waist")) return MuscleGroup.Abs;
        if (t.Contains("calves")) return MuscleGroup.Calves;
        if (t.Contains("upper back")) return MuscleGroup.UpperBack;

        return MuscleGroup.Glutes;
    }

    private static string Normalize(string text)
    {
        return string.Concat(text.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    }

    private static string FormatTitle(string text)
    {
        return char.ToUpperInvariant(text[0]) + (text.Length > 1 ? text[1..] : string.Empty);
    }

    private static string StripHtml(string html)
    {
        var array = new char[html.Length];
        var arrayIndex = 0;
        var inside = false;

        for (var i = 0; i < html.Length; i++)
        {
            var let = html[i];
            if (let == '<')
            {
                inside = true;
                continue;
            }

            if (let == '>')
            {
                inside = false;
                continue;
            }

            if (!inside)
            {
                array[arrayIndex] = let;
                arrayIndex++;
            }
        }

        return new string(array, 0, arrayIndex).Trim();
    }
}
