using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Data;

public class AppDatabase : DbContext
{
    public AppDatabase(DbContextOptions<AppDatabase> options) : base(options) { }

    // User domain
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserProfileSnapshot> UserProfileSnapshots => Set<UserProfileSnapshot>();

    // Quiz domain
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAnswer> QuizAnswers => Set<QuizAnswer>();
    public DbSet<UserQuizResponse> UserQuizResponses => Set<UserQuizResponse>();

    // Cycle domain
    public DbSet<CycleLog> CycleLogs => Set<CycleLog>();
    public DbSet<SymptomLog> SymptomLogs => Set<SymptomLog>();

    // Nutrition domain
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<FoodItemVersion> FoodItemVersions => Set<FoodItemVersion>();
    public DbSet<FoodSyncLog> FoodSyncLogs => Set<FoodSyncLog>();
    public DbSet<MealTemplate> MealTemplates => Set<MealTemplate>();
    public DbSet<MealTemplateEntry> MealTemplateEntries => Set<MealTemplateEntry>();
    public DbSet<LoggedMeal> LoggedMeals => Set<LoggedMeal>();
    public DbSet<LoggedMealEntry> LoggedMealEntries => Set<LoggedMealEntry>();

    // Workout domain
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();
    public DbSet<WorkoutDay> WorkoutDays => Set<WorkoutDay>();
    public DbSet<WorkoutDayExercise> WorkoutDayExercises => Set<WorkoutDayExercise>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();

    // Recommendation domain
    public DbSet<RecommendationSet> RecommendationSets => Set<RecommendationSet>();
    public DbSet<NutritionRecommendation> NutritionRecommendations => Set<NutritionRecommendation>();
    public DbSet<WorkoutRecommendation> WorkoutRecommendations => Set<WorkoutRecommendation>();
    public DbSet<WellnessRecommendation> WellnessRecommendations => Set<WellnessRecommendation>();

    public async Task<bool> AreExercisesSeededAsync() =>
        await Exercises.AnyAsync();
    
    public async Task<bool> AreQuestionsSeededAsync() =>
        await QuizQuestions.AnyAsync();

    public async Task SeedQuizAsync()
    {
        if (await QuizQuestions.AnyAsync()) return;

        var questions = new List<QuizQuestion>
        {
            new QuizQuestion
            {
                Question = "How is your energy level during your period?",
                OrderIndex = 1,
                QuestionType = QuizQuestionType.MenstrualEnergy,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "Very low — I struggle to move", OrderIndex = 1, MappedValue = 1 },
                    new QuizAnswer { Text = "Low — light movement only",     OrderIndex = 2, MappedValue = 2 },
                    new QuizAnswer { Text = "Moderate — I can train lightly",OrderIndex = 3, MappedValue = 3 },
                    new QuizAnswer { Text = "Normal — it doesn't affect me", OrderIndex = 4, MappedValue = 4 },
                }
            },
            new QuizQuestion
            {
                Question = "How intense are your menstrual cramps typically?",
                OrderIndex = 2,
                QuestionType = QuizQuestionType.MenstrualPain,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "None",                    OrderIndex = 1, MappedValue = 1 },
                    new QuizAnswer { Text = "Mild — barely noticeable",OrderIndex = 2, MappedValue = 2 },
                    new QuizAnswer { Text = "Moderate — uncomfortable",OrderIndex = 3, MappedValue = 3 },
                    new QuizAnswer { Text = "Severe — affects my day", OrderIndex = 4, MappedValue = 4 },
                }
            },
            new QuizQuestion
            {
                Question = "In the week after your period, how does your energy usually shift?",
                OrderIndex = 3,
                QuestionType = QuizQuestionType.FollicularEnergy,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "Big boost — I feel great",     OrderIndex = 1, MappedValue = 4 },
                    new QuizAnswer { Text = "Slight boost",                 OrderIndex = 2, MappedValue = 3 },
                    new QuizAnswer { Text = "No noticeable change",         OrderIndex = 3, MappedValue = 2 },
                    new QuizAnswer { Text = "Still low energy",             OrderIndex = 4, MappedValue = 1 },
                }
            },
            new QuizQuestion
            {
                Question = "Around ovulation (mid-cycle), how do you usually feel?",
                OrderIndex = 4,
                QuestionType = QuizQuestionType.OvulatoryEnergy,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "Peak energy and motivation",   OrderIndex = 1, MappedValue = 4 },
                    new QuizAnswer { Text = "Pretty good",                  OrderIndex = 2, MappedValue = 3 },
                    new QuizAnswer { Text = "Neutral — same as usual",      OrderIndex = 3, MappedValue = 2 },
                    new QuizAnswer { Text = "Tired or uncomfortable",       OrderIndex = 4, MappedValue = 1 },
                }
            },
            new QuizQuestion
            {
                Question = "In the week before your period, how does your energy tend to feel?",
                OrderIndex = 5,
                QuestionType = QuizQuestionType.LutealEnergy,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "Strong and focused",           OrderIndex = 1, MappedValue = 4 },
                    new QuizAnswer { Text = "Slightly lower than normal",   OrderIndex = 2, MappedValue = 3 },
                    new QuizAnswer { Text = "Significantly more tired",     OrderIndex = 3, MappedValue = 2 },
                    new QuizAnswer { Text = "Very low — hard to train",     OrderIndex = 4, MappedValue = 1 },
                }
            },
            new QuizQuestion
            {
                Question = "Do you experience physical discomfort in the week before your period?",
                OrderIndex = 6,
                QuestionType = QuizQuestionType.LutealPain,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "None",                         OrderIndex = 1, MappedValue = 1 },
                    new QuizAnswer { Text = "Mild bloating or cramping",    OrderIndex = 2, MappedValue = 2 },
                    new QuizAnswer { Text = "Moderate pain or tension",     OrderIndex = 3, MappedValue = 3 },
                    new QuizAnswer { Text = "Severe — affects my daily life",OrderIndex = 4, MappedValue = 4 },
                }
            },
            new QuizQuestion
            {
                Question = "Which symptom most disrupts your workouts during your cycle?",
                OrderIndex = 7,
                QuestionType = QuizQuestionType.CycleSymptoms,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "Fatigue",          OrderIndex = 1, MappedValue = 1 },
                    new QuizAnswer { Text = "Cramps",           OrderIndex = 2, MappedValue = 2 },
                    new QuizAnswer { Text = "Mood changes",     OrderIndex = 3, MappedValue = 3 },
                    new QuizAnswer { Text = "Bloating",         OrderIndex = 4, MappedValue = 4 },
                    new QuizAnswer { Text = "None significantly",OrderIndex = 5, MappedValue = 0 },
                }
            },
            new QuizQuestion
            {
                Question = "What is your primary fitness goal?",
                OrderIndex = 8,
                QuestionType = QuizQuestionType.Goal,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "Lose fat",                 OrderIndex = 1, MappedValue = (int)UserGoal.FatLoss },
                    new QuizAnswer { Text = "Get toned",                OrderIndex = 2, MappedValue = (int)UserGoal.MuscleTone },
                    new QuizAnswer { Text = "Build strength",           OrderIndex = 3, MappedValue = (int)UserGoal.Strength },
                    new QuizAnswer { Text = "Stay healthy and active",  OrderIndex = 4, MappedValue = (int)UserGoal.MaintainHealth },
                }
            },
            new QuizQuestion
            {
                Question = "How many days per week do you want active workouts?",
                OrderIndex = 9,
                QuestionType = QuizQuestionType.WorkoutDaysPerWeek,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "2 days", OrderIndex = 1, MappedValue = 2 },
                    new QuizAnswer { Text = "3 days", OrderIndex = 2, MappedValue = 3 },
                    new QuizAnswer { Text = "4 days", OrderIndex = 3, MappedValue = 4 },
                    new QuizAnswer { Text = "5 days", OrderIndex = 4, MappedValue = 5 },
                }
            },
            new QuizQuestion
            {
                Question = "Which type of active workout do you prefer?",
                OrderIndex = 10,
                QuestionType = QuizQuestionType.WorkoutTypePreference,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "Strength training",  OrderIndex = 1, MappedValue = 1 },
                    new QuizAnswer { Text = "Cardio",             OrderIndex = 2, MappedValue = 2 },
                    new QuizAnswer { Text = "Mix of both",        OrderIndex = 3, MappedValue = 3 },
                }
            },
            new QuizQuestion
            {
                Question = "How would you describe your current fitness level?",
                OrderIndex = 11,
                QuestionType = QuizQuestionType.ExperienceLevel,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "Beginner — just starting out",   OrderIndex = 1, MappedValue = 1 },
                    new QuizAnswer { Text = "Intermediate — train regularly", OrderIndex = 2, MappedValue = 2 },
                    new QuizAnswer { Text = "Advanced — experienced athlete", OrderIndex = 3, MappedValue = 3 },
                }
            },
            new QuizQuestion
            {
                Question = "Do you experience joint sensitivity or pain during your cycle?",
                OrderIndex = 12,
                QuestionType = QuizQuestionType.FollicularPain,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "Never",       OrderIndex = 1, MappedValue = 1 },
                    new QuizAnswer { Text = "Occasionally",OrderIndex = 2, MappedValue = 2 },
                    new QuizAnswer { Text = "Often",       OrderIndex = 3, MappedValue = 3 },
                    new QuizAnswer { Text = "Always",      OrderIndex = 4, MappedValue = 4 },
                }
            },
            new QuizQuestion
            {
                Question = "Do you experience mid-cycle discomfort (around ovulation)?",
                OrderIndex = 13,
                QuestionType = QuizQuestionType.OvulatoryPain,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "Never",     OrderIndex = 1, MappedValue = 1 },
                    new QuizAnswer { Text = "Sometimes", OrderIndex = 2, MappedValue = 2 },
                    new QuizAnswer { Text = "Yes, often",OrderIndex = 3, MappedValue = 3 },
                }
            },
            new QuizQuestion
            {
                Question = "Do you follow any dietary preferences?",
                OrderIndex = 14,
                QuestionType = QuizQuestionType.DietaryPreference,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "No restrictions",  OrderIndex = 1, MappedValue = (int)DietaryTag.None },
                    new QuizAnswer { Text = "Vegetarian",       OrderIndex = 2, MappedValue = (int)DietaryTag.Vegetarian },
                    new QuizAnswer { Text = "Vegan",            OrderIndex = 3, MappedValue = (int)DietaryTag.Vegan },
                    new QuizAnswer { Text = "Gluten-free",      OrderIndex = 4, MappedValue = (int)DietaryTag.GlutenFree },
                    new QuizAnswer { Text = "Lactose-free",     OrderIndex = 5, MappedValue = (int)DietaryTag.LactoseFree },
                }
            },
            new QuizQuestion
            {
                Question = "How would you rate your current stress level?",
                OrderIndex = 15,
                QuestionType = QuizQuestionType.LifestyleStress,
                Answers = new List<QuizAnswer>
                {
                    new QuizAnswer { Text = "Low — I feel balanced",    OrderIndex = 1, MappedValue = 1 },
                    new QuizAnswer { Text = "Moderate",                 OrderIndex = 2, MappedValue = 2 },
                    new QuizAnswer { Text = "High — often stressed",    OrderIndex = 3, MappedValue = 3 },
                    new QuizAnswer { Text = "Very high",                OrderIndex = 4, MappedValue = 4 },
                }
            },
        };

        await QuizQuestions.AddRangeAsync(questions);
        await SaveChangesAsync();
    } 

    public async Task SeedExercisesAsync()
    {
        var exercises = new List<Exercise>
        {
            // STRENGTH – LOWER BODY
            new Exercise { Code = "LEG_PRESS",            Name = "Leg Press (Machine)",           Description = "Sit with back flat against the pad, feet hip-width on the platform, lower the sled by bending knees, then press through heels to return without locking knees.",                                              PrimaryMuscle = MuscleGroup.Quads,         SecondaryMuscles = "Glutes,Hamstrings",           JointAreas = "Knee,Hip",         IsInjuryFriendly = true  },
            new Exercise { Code = "GOBLET_SQUAT",         Name = "Goblet Squat",                  Description = "Hold a dumbbell close to your chest, stand shoulder-width, sit hips down and back keeping chest tall, then drive through mid-foot to stand.",                                                              PrimaryMuscle = MuscleGroup.Quads,         SecondaryMuscles = "Glutes,Hamstrings,Core",      JointAreas = "Knee,Hip,LowerBack", IsInjuryFriendly = false },
            new Exercise { Code = "SPLIT_SQUAT_BENCH",    Name = "Supported Split Squat",         Description = "Stand in a split stance with one hand lightly holding a rail or rack, lower back knee toward the floor keeping front knee over mid-foot, push through front heel to stand.",                               PrimaryMuscle = MuscleGroup.Quads,         SecondaryMuscles = "Glutes,Hamstrings",           JointAreas = "Knee,Hip",         IsInjuryFriendly = true  },
            new Exercise { Code = "SEATED_LEG_CURL",      Name = "Seated Leg Curl (Machine)",     Description = "Sit with knees aligned to the machine's pivot, lock pad just above ankles, curl heels down under the seat with control, then return without slamming the weight.",                                        PrimaryMuscle = MuscleGroup.Hamstrings,    SecondaryMuscles = "Calves",                      JointAreas = "Knee",             IsInjuryFriendly = true  },
            new Exercise { Code = "DB_ROMANIAN_DEADLIFT", Name = "Romanian Deadlift (Dumbbells)", Description = "Hold dumbbells in front of thighs, soften knees, hinge hips back keeping spine neutral, feel a stretch in hamstrings, then drive hips forward to stand tall.",                                            PrimaryMuscle = MuscleGroup.Hamstrings,    SecondaryMuscles = "Glutes,LowerBack",            JointAreas = "Hip,LowerBack",    IsInjuryFriendly = false },
            new Exercise { Code = "SLIDER_HAMSTRING_BRIDGE", Name = "Sliding Hamstring Bridge",   Description = "Lie on your back with heels on sliders, lift hips into a bridge, slowly extend legs out, then curl heels back toward glutes while keeping hips up.",                                                     PrimaryMuscle = MuscleGroup.Hamstrings,    SecondaryMuscles = "Glutes,LowerBack",            JointAreas = "Hip",              IsInjuryFriendly = true  },
            new Exercise { Code = "BARBELL_HIP_THRUST",   Name = "Barbell Hip Thrust",            Description = "Upper back on a bench, barbell across hips, feet flat, drop hips down under control, then drive hips up squeezing glutes hard at the top without overextending back.",                                    PrimaryMuscle = MuscleGroup.Glutes,        SecondaryMuscles = "Hamstrings,LowerBack",        JointAreas = "Hip,LowerBack",    IsInjuryFriendly = false },
            new Exercise { Code = "GLUTE_BRIDGE_BODYWEIGHT", Name = "Glute Bridge (Bodyweight)",  Description = "Lie on your back with knees bent and feet flat, brace core, press through heels to lift hips until body forms a straight line from shoulders to knees, lower slowly.",                                    PrimaryMuscle = MuscleGroup.Glutes,        SecondaryMuscles = "Hamstrings,LowerBack",        JointAreas = "Hip",              IsInjuryFriendly = true  },
            new Exercise { Code = "CABLE_KICKBACK",       Name = "Cable Glute Kickback",          Description = "Attach ankle strap to low cable, stand holding the column for support, extend working leg back and slightly out while squeezing glute, return with control.",                                              PrimaryMuscle = MuscleGroup.Glutes,        SecondaryMuscles = "Hamstrings",                  JointAreas = "Hip",              IsInjuryFriendly = true  },
            new Exercise { Code = "SEATED_CALF_RAISE_MACHINE", Name = "Seated Calf Raise (Machine)", Description = "Sit with knees under pads and balls of feet on platform, lower heels toward floor, then press through toes to raise heels as high as possible.",                                                      PrimaryMuscle = MuscleGroup.Calves,        SecondaryMuscles = "",                            JointAreas = "Ankle",            IsInjuryFriendly = true  },
            new Exercise { Code = "SEATED_ADDUCTOR_MACHINE", Name = "Seated Hip Adductor (Machine)", Description = "Sit with inner thighs against pads, feet on pegs, squeeze legs together in a slow, controlled motion, then return just until tension remains.",                                                       PrimaryMuscle = MuscleGroup.Adductors,     SecondaryMuscles = "HipFlexors",                  JointAreas = "Hip",              IsInjuryFriendly = true  },

            // STRENGTH – UPPER BODY
            new Exercise { Code = "CHEST_PRESS_MACHINE",  Name = "Chest Press (Machine)",         Description = "Sit with back on pad and handles at mid-chest level, press handles forward until arms are almost straight, then return until elbows are just past your torso.",                                            PrimaryMuscle = MuscleGroup.Chest,         SecondaryMuscles = "Triceps,FrontShoulders",      JointAreas = "Shoulder,Elbow",   IsInjuryFriendly = true  },
            new Exercise { Code = "INCLINE_PUSH_UP_BENCH",Name = "Incline Push-Up on Bench",      Description = "Place hands on a bench, walk feet back into a straight line, lower chest toward bench keeping elbows at about 45 degrees, push away to straight arms.",                                                   PrimaryMuscle = MuscleGroup.Chest,         SecondaryMuscles = "Triceps,FrontShoulders,Core", JointAreas = "Shoulder,Wrist",   IsInjuryFriendly = true  },
            new Exercise { Code = "LAT_PULLDOWN",         Name = "Lat Pulldown (Cable)",          Description = "Sit tall with knees under pad, grasp bar slightly wider than shoulders, pull bar toward upper chest while driving elbows down, control the return to full stretch.",                                       PrimaryMuscle = MuscleGroup.UpperBack,     SecondaryMuscles = "Biceps,RearShoulders",        JointAreas = "Shoulder,Elbow",   IsInjuryFriendly = true  },
            new Exercise { Code = "CHEST_SUPPORTED_DB_ROW", Name = "Chest-Supported Dumbbell Row", Description = "Lie chest-down on an incline bench holding dumbbells, start with arms straight, row dumbbells toward hips while squeezing shoulder blades, lower slowly.",                                             PrimaryMuscle = MuscleGroup.UpperBack,     SecondaryMuscles = "Biceps,RearShoulders",        JointAreas = "Shoulder",         IsInjuryFriendly = true  },
            new Exercise { Code = "SHOULDER_PRESS_MACHINE", Name = "Shoulder Press (Machine)",    Description = "Sit with back flat, handles at or just below chin height, press overhead without locking elbows, lower until elbows are about 90 degrees.",                                                               PrimaryMuscle = MuscleGroup.FrontShoulders,SecondaryMuscles = "SideShoulders,Triceps",       JointAreas = "Shoulder,Elbow",   IsInjuryFriendly = true  },
            new Exercise { Code = "CABLE_TRICEP_PRESSDOWN", Name = "Cable Triceps Pressdown",     Description = "Stand facing a high cable with bar or rope, elbows close to sides, press handle down until arms are straight, then return to about 90 degrees.",                                                          PrimaryMuscle = MuscleGroup.Triceps,       SecondaryMuscles = "Forearms",                    JointAreas = "Elbow",            IsInjuryFriendly = true  },
            new Exercise { Code = "CABLE_BICEP_CURL",     Name = "Cable Biceps Curl",             Description = "Attach a straight or EZ bar to a low cable, stand upright, curl bar toward chest keeping elbows close to sides, then lower slowly.",                                                                     PrimaryMuscle = MuscleGroup.Biceps,        SecondaryMuscles = "Forearms",                    JointAreas = "Elbow,Wrist",      IsInjuryFriendly = true  },

            // CORE
            new Exercise { Code = "PLANK",                Name = "Plank",                         Description = "Place forearms on the floor with elbows under shoulders, extend legs back, keep body in a straight line from head to heels without letting hips sag or pike.",                                            PrimaryMuscle = MuscleGroup.Abs,           SecondaryMuscles = "Obliques,LowerBack",          JointAreas = "",                 IsInjuryFriendly = true  },
            new Exercise { Code = "DEAD_BUG",             Name = "Dead Bug",                      Description = "Lie on back with arms toward ceiling and knees bent at 90 degrees, slowly lower opposite arm and leg toward floor while keeping low back pressed down, then switch sides.",                                PrimaryMuscle = MuscleGroup.Abs,           SecondaryMuscles = "HipFlexors",                  JointAreas = "",                 IsInjuryFriendly = true  },
            new Exercise { Code = "BIRD_DOG",             Name = "Bird Dog",                      Description = "On hands and knees, extend opposite arm and leg while keeping spine neutral, pause, then return and switch sides without letting hips twist.",                                                             PrimaryMuscle = MuscleGroup.LowerBack,     SecondaryMuscles = "Glutes,Abs",                  JointAreas = "",                 IsInjuryFriendly = true  },

            // LOW-IMPACT CARDIO
            new Exercise { Code = "TREADMILL_INCLINE_WALK", Name = "Treadmill Incline Walk",      Description = "Walk on a treadmill at a comfortable speed with a slight incline, land softly on mid-foot, keep posture tall and arms relaxed.",                                                                          PrimaryMuscle = MuscleGroup.Calves,        SecondaryMuscles = "Quads,Glutes",                JointAreas = "Knee,Ankle,Hip",   IsInjuryFriendly = true  },
            new Exercise { Code = "STATIONARY_BIKE",      Name = "Stationary Bike",               Description = "Sit with slight bend in knees at bottom of pedal stroke, pedal smoothly at moderate resistance, avoid rocking hips.",                                                                                    PrimaryMuscle = MuscleGroup.Quads,         SecondaryMuscles = "Glutes,Calves",               JointAreas = "Knee,Hip,Ankle",   IsInjuryFriendly = true  },
            new Exercise { Code = "ELLIPTICAL_TRAINER",   Name = "Elliptical Trainer",            Description = "Stand upright holding handles lightly, move feet in smooth oval pattern, push and pull handles with arms for a full-body low-impact rhythm.",                                                             PrimaryMuscle = MuscleGroup.Quads,         SecondaryMuscles = "Glutes,Calves,UpperBack",     JointAreas = "Knee,Hip,Ankle,Shoulder", IsInjuryFriendly = true },
            new Exercise { Code = "ROWING_MACHINE",       Name = "Rowing Machine",                Description = "Start with knees bent and torso slightly forward, push through legs then lean back slightly and pull handle to ribs, reverse in the same order.",                                                         PrimaryMuscle = MuscleGroup.UpperBack,     SecondaryMuscles = "Quads,Glutes,Abs,Biceps",     JointAreas = "Knee,Hip,Shoulder",IsInjuryFriendly = true  },
            new Exercise { Code = "STAIR_CLIMBER",        Name = "Stair Climber",                 Description = "Hold rails lightly for balance, step in a smooth climbing motion, keep torso tall and avoid pounding feet.",                                                                                              PrimaryMuscle = MuscleGroup.Quads,         SecondaryMuscles = "Glutes,Calves",               JointAreas = "Knee,Ankle,Hip",   IsInjuryFriendly = true  },

            // RECOVERY & MOBILITY
            new Exercise { Code = "CHILDS_POSE",          Name = "Child's Pose",                  Description = "From kneeling, sit hips back toward heels, reach arms forward, relax chest toward floor and breathe slowly.",                                                                                            PrimaryMuscle = MuscleGroup.LowerBack,     SecondaryMuscles = "HipFlexors,Shoulders",        JointAreas = "",                 IsInjuryFriendly = true  },
            new Exercise { Code = "CAT_COW",              Name = "Cat-Cow Stretch",               Description = "On hands and knees, alternate rounding spine up toward ceiling and gently arching chest forward while looking slightly up.",                                                                              PrimaryMuscle = MuscleGroup.LowerBack,     SecondaryMuscles = "Abs",                         JointAreas = "",                 IsInjuryFriendly = true  },
            new Exercise { Code = "PIGEON_POSE",          Name = "Pigeon Pose",                   Description = "From a plank or downward dog, bring one knee forward and place shin across mat, slide back leg behind, gently lower hips toward floor.",                                                                 PrimaryMuscle = MuscleGroup.HipFlexors,    SecondaryMuscles = "Glutes",                      JointAreas = "Hip",              IsInjuryFriendly = true  },
            new Exercise { Code = "SUPINE_SPINAL_TWIST",  Name = "Supine Spinal Twist",           Description = "Lie on back, bring knees to chest, let them drop to one side while keeping shoulders on floor, turn head opposite, then switch.",                                                                        PrimaryMuscle = MuscleGroup.Obliques,      SecondaryMuscles = "LowerBack",                   JointAreas = "",                 IsInjuryFriendly = true  },
            new Exercise { Code = "LEGS_UP_WALL",         Name = "Legs Up the Wall",              Description = "Lie on back with hips near a wall, extend legs up the wall, relax arms and breathe slowly.",                                                                                                             PrimaryMuscle = MuscleGroup.LowerBack,     SecondaryMuscles = "Hamstrings",                  JointAreas = "",                 IsInjuryFriendly = true  },
            new Exercise { Code = "BUTTERFLY_STRETCH",    Name = "Butterfly Stretch",             Description = "Sit tall with soles of feet together and knees dropping out to sides, hold feet and gently draw chest forward.",                                                                                         PrimaryMuscle = MuscleGroup.Adductors,     SecondaryMuscles = "HipFlexors",                  JointAreas = "Hip",              IsInjuryFriendly = true  },
            new Exercise { Code = "GENTLE_WALK_OUTDOORS", Name = "Gentle Walk Outdoors",          Description = "Walk at an easy pace focusing on relaxed breathing, soft foot strikes, and comfortable posture.",                                                                                                         PrimaryMuscle = MuscleGroup.Calves,        SecondaryMuscles = "Quads,Glutes",                JointAreas = "",                 IsInjuryFriendly = true  }
        };

        await Exercises.AddRangeAsync(exercises);
        await SaveChangesAsync();
    }
}