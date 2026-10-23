using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

namespace MuscleCuties.Infrastructure.PostgreSql.Nutrition.Diary;

internal static class PostgresNutritionDiaryEventWriter
{
    public static async Task AppendAsync(
        PostgresDbContext context,
        Guid userId,
        DateOnly diaryDate,
        string commandId,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO app.nutrition_diary_event_cursors (id, user_id, last_cursor) VALUES ({userId}, {userId}, 0) ON CONFLICT (user_id) DO NOTHING",
            cancellationToken).ConfigureAwait(false);
        var cursorRow = await context.NutritionDiaryEventCursors
            .FromSqlInterpolated($"SELECT id, user_id, last_cursor FROM app.nutrition_diary_event_cursors WHERE user_id = {userId} FOR UPDATE")
            .SingleAsync(cancellationToken).ConfigureAwait(false);
        var nextCursor = checked(cursorRow.LastCursor + 1);
        cursorRow.LastCursor = nextCursor;
        context.NutritionDiaryCommandEvents.Add(new NutritionDiaryCommandEventRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Cursor = nextCursor,
            DiaryDate = diaryDate,
            CommandId = commandId
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
