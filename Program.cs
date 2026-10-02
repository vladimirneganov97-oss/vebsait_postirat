using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using LaundrySite.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var dbPath = Environment.GetEnvironmentVariable("LAUNDRY_DB_PATH")
             ?? Path.Combine("Data", "laundry.db");

var dbDirectory = Path.GetDirectoryName(Path.GetFullPath(dbPath));
if (!string.IsNullOrWhiteSpace(dbDirectory))
    Directory.CreateDirectory(dbDirectory);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(30);
    options.Cookie.Name = "__LaundrySite.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    if (builder.Environment.IsProduction())
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

// A deleted user must immediately lose access even if their old session cookie remains.
// Also compare PublicId so a recycled database integer ID can never inherit an old session.
app.Use(async (context, next) =>
{
    var userId = context.Session.GetInt32("UserId");
    if (userId.HasValue)
    {
        var db = context.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value);
        var sessionPublicId = context.Session.GetString("UserPublicId");

        if (user == null || !string.Equals(user.PublicId, sessionPublicId, StringComparison.Ordinal))
            context.Session.Clear();
    }

    await next();
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    EnsureColumn(db, "Users", "PublicId", "TEXT NOT NULL DEFAULT ''");
    EnsureColumn(db, "Users", "WeeklyBookingLimit", "INTEGER NOT NULL DEFAULT 2");
    EnsureColumn(db, "Users", "PasswordHash", "TEXT NOT NULL DEFAULT ''");
    EnsureColumn(db, "WashSlots", "ReservationWeekStart", "TEXT NULL");
    EnsureColumn(db, "WashSlots", "ReservedByUserId", "INTEGER NULL");
    EnsureColumn(db, "WashSlots", "Status", "TEXT NOT NULL DEFAULT 'Свободно'");

    // Give every old account a new public login ID.
    var users = db.Users.ToList();
    foreach (var user in users)
    {
        if (string.IsNullOrWhiteSpace(user.PublicId))
            user.PublicId = CreateUniquePublicId(db);

        if (user.WeeklyBookingLimit < 0)
            user.WeeklyBookingLimit = 2;
    }
    db.SaveChanges();

    // Link old text-based reservations to the corresponding user.
    var slots = db.WashSlots.ToList();
    foreach (var slot in slots.Where(s => s.ReservedByUserId == null && !string.IsNullOrWhiteSpace(s.ReservedBy)))
    {
        var user = users.FirstOrDefault(u =>
            $"{u.FirstName} {u.LastName} (комната {u.RoomNumber})" == slot.ReservedBy);
        if (user != null)
            slot.ReservedByUserId = user.Id;
    }
    db.SaveChanges();

    // PublicId is not a secret, but it must be unique.
    using var connection = db.Database.GetDbConnection();
    connection.Open();
    using var index = connection.CreateCommand();
    index.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_PublicId ON Users(PublicId);";
    index.ExecuteNonQuery();

    SeedData.Initialize(db);
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static string CreateUniquePublicId(AppDbContext db)
{
    string id;
    do
    {
        id = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
    }
    while (db.Users.Any(u => u.PublicId == id));

    return id;
}

static void EnsureColumn(AppDbContext db, string table, string column, string definition)
{
    using var connection = db.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
        connection.Open();

    using var command = connection.CreateCommand();
    command.CommandText = $"PRAGMA table_info({table});";
    using var reader = command.ExecuteReader();

    while (reader.Read())
    {
        if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            return;
    }

    reader.Close();

    using var alter = connection.CreateCommand();
    alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
    alter.ExecuteNonQuery();
}
