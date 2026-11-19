using System.Text.Json;
using Microsoft.Data.Sqlite;
namespace StraussDrive.Api.Advisor;

/// <summary>Versioned SQLite schema and atomic conversation/ticket writes. No payroll fields in audits or ticket context.</summary>
public sealed partial class AdvisorStore
{
    public string Path { get; }
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public AdvisorStore(IConfiguration configuration, IWebHostEnvironment environment)
    {
        Path = System.IO.Path.GetFullPath(configuration["Advisor:DatabasePath"] ?? System.IO.Path.Combine(environment.ContentRootPath, ".local", "advisor.db"));
        var parent = System.IO.Path.GetDirectoryName(Path)!;
        var existed = Directory.Exists(parent); Directory.CreateDirectory(parent);
        if (!existed && !OperatingSystem.IsWindows()) File.SetUnixFileMode(parent, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        if (Convert.ToInt32(command.ExecuteScalar()) > 2) throw new InvalidOperationException("Advisor database schema is newer than this application.");
        command.CommandText = "PRAGMA journal_mode=WAL;"; command.ExecuteScalar();
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        }
        using var transaction = db.BeginTransaction();
        Execute(db, transaction, """
            CREATE TABLE IF NOT EXISTS SchemaMigrations(Version INTEGER PRIMARY KEY, AppliedAt TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS EmployeeSession(Id TEXT PRIMARY KEY, ProfileKey TEXT NOT NULL, CreatedAt TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS Conversation(Id TEXT PRIMARY KEY, OwnerId TEXT NOT NULL REFERENCES EmployeeSession(Id), ProfileKey TEXT NOT NULL, Title TEXT NOT NULL, State TEXT NOT NULL, Version INTEGER NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS IX_Conversation_Owner ON Conversation(OwnerId,UpdatedAt);
            CREATE TABLE IF NOT EXISTS ConversationMessage(Id TEXT PRIMARY KEY, ConversationId TEXT NOT NULL REFERENCES Conversation(Id), Sequence INTEGER NOT NULL, Payload TEXT NOT NULL, UNIQUE(ConversationId,Sequence));
            CREATE TABLE IF NOT EXISTS RepresentativeTicket(Id TEXT PRIMARY KEY, ConversationId TEXT NOT NULL REFERENCES Conversation(Id), Status TEXT NOT NULL, Payload TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS IX_Ticket_Conversation ON RepresentativeTicket(ConversationId);
            CREATE TABLE IF NOT EXISTS RepresentativeResponse(Id TEXT PRIMARY KEY, TicketId TEXT NOT NULL REFERENCES RepresentativeTicket(Id), Sequence INTEGER NOT NULL, Payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS AuditEvent(Id TEXT PRIMARY KEY, TicketId TEXT NOT NULL REFERENCES RepresentativeTicket(Id), Sequence INTEGER NOT NULL, Payload TEXT NOT NULL);
            INSERT OR IGNORE INTO SchemaMigrations VALUES(1,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            CREATE TABLE IF NOT EXISTS SavedScenario(Id TEXT PRIMARY KEY, OwnerId TEXT NOT NULL REFERENCES EmployeeSession(Id), ProfileKey TEXT NOT NULL, Name TEXT NOT NULL, Request TEXT NOT NULL, SourceFingerprint TEXT NOT NULL, CreatedAt TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS IX_SavedScenario_Owner ON SavedScenario(OwnerId,CreatedAt);
            INSERT OR IGNORE INTO SchemaMigrations VALUES(2,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            PRAGMA user_version=2;
            """);
        transaction.Commit();
    }
    private SqliteConnection Open() { var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path, ForeignKeys = true, DefaultTimeout = 10 }.ToString()); db.Open(); return db; }
    private static int Execute(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object?)[] args)
    { using var c = db.CreateCommand(); c.Transaction = tx; c.CommandText = sql; foreach (var (key, value) in args) c.Parameters.AddWithValue(key, value ?? DBNull.Value); return c.ExecuteNonQuery(); }
    private static List<T> Query<T>(SqliteConnection db, SqliteTransaction? tx, string sql, Func<SqliteDataReader, T> map, params (string, object?)[] args)
    { using var c = db.CreateCommand(); c.Transaction = tx; c.CommandText = sql; foreach (var (k, v) in args) c.Parameters.AddWithValue(k, v ?? DBNull.Value); using var reader = c.ExecuteReader(); var rows = new List<T>(); while (reader.Read()) rows.Add(map(reader)); return rows; }
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Json)!;
    public static string Now() => DateTimeOffset.UtcNow.ToString("O");
    public static string Id() => Guid.NewGuid().ToString("N");
    public EmployeeSession? Session(string id) { using var db = Open(); return Query(db, null, "SELECT Id,ProfileKey,CreatedAt FROM EmployeeSession WHERE Id=$id AND ProfileKey <> 'revoked'", r => new EmployeeSession(r.GetString(0), r.GetString(1), r.GetString(2)), ("$id", id)).SingleOrDefault(); }
    // Keep historical conversations and foreign keys; revoke the identity, not its records.
    public void RevokeSession(string id) => RevokeSessions([id]);
    public void RevokeSessions(IEnumerable<string> ids)
    {
        using var db = Open(); using var tx = db.BeginTransaction();
        foreach (var id in ids.Distinct()) Execute(db, tx, "UPDATE EmployeeSession SET ProfileKey='revoked' WHERE Id=$id", ("$id", id));
        tx.Commit();
    }
    public EmployeeSession CreateSession(string profile) { var s = new EmployeeSession(Id(), profile, Now()); using var db = Open(); Execute(db, null, "INSERT INTO EmployeeSession VALUES($id,$p,$at)", ("$id", s.Id), ("$p", profile), ("$at", s.CreatedAt)); return s; }
    private static Conversation MapConversation(SqliteDataReader r) => new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), Deserialize<ConversationState>(r.GetString(4)), r.GetInt32(5), r.GetString(6), r.GetString(7));
    public Conversation RequireConversation(string id, string owner) { using var db = Open(); return Query(db, null, "SELECT * FROM Conversation WHERE Id=$id AND OwnerId=$owner", MapConversation, ("$id", id), ("$owner", owner)).SingleOrDefault() ?? throw new AdvisorException(404, "השיחה לא נמצאה בחשבון שלך."); }
    public Conversation[] List(string owner) { using var db = Open(); return Query(db, null, "SELECT * FROM Conversation WHERE OwnerId=$o ORDER BY UpdatedAt DESC", MapConversation, ("$o", owner)).ToArray(); }
    public Conversation Create(EmployeeSession session, ConversationState state, ConversationMessage welcome)
    {
        var at = Now(); var c = new Conversation(Id(), session.Id, session.ProfileKey, "בדיקת הטבת רכב", state, 0, at, at); using var db = Open(); using var tx = db.BeginTransaction();
        Execute(db, tx, "INSERT INTO Conversation VALUES($id,$o,$p,$title,$state,0,$at,$at)", ("$id", c.Id), ("$o", c.OwnerId), ("$p", c.ProfileKey), ("$title", c.Title), ("$state", Serialize(state)), ("$at", at)); AddMessage(db, tx, c.Id, welcome); tx.Commit(); return c;
    }
    private static void AddMessage(SqliteConnection db, SqliteTransaction tx, string conversationId, ConversationMessage message) => Execute(db, tx, "INSERT INTO ConversationMessage VALUES($id,$c,(SELECT COALESCE(MAX(Sequence),0)+1 FROM ConversationMessage WHERE ConversationId=$c),$payload)", ("$id", message.Id), ("$c", conversationId), ("$payload", Serialize(message)));
    private static void SaveTicket(SqliteConnection db, SqliteTransaction tx, RepresentativeTicket ticket) => Execute(db, tx, "INSERT INTO RepresentativeTicket VALUES($id,$c,$s,$p) ON CONFLICT(Id) DO UPDATE SET Status=$s,Payload=$p", ("$id", ticket.Id), ("$c", ticket.ConversationId), ("$s", ticket.Status), ("$p", Serialize(ticket)));
    public void Save(Conversation c, int expectedVersion, ConversationMessage user, AgentOutcome outcome)
    {
        using var db = Open(); using var tx = db.BeginTransaction();
        UpdateConversation(db, tx, c, expectedVersion);
        AddMessage(db, tx, c.Id, user); AddMessage(db, tx, c.Id, outcome.Message);
        if (outcome.NewTicket is { } ticket)
        {
            if (ticket.ConversationId != c.Id) throw new AdvisorException(404, "הפנייה אינה שייכת לשיחה.");
            SaveTicket(db, tx, ticket);
            if (outcome.Message.Mode == "employee-confirmed")
            {
                var audit = new AuditEvent(Id(), ticket.Id, c.OwnerId, "employee", "request_created", Now());
                Execute(db, tx, "INSERT INTO AuditEvent VALUES($id,$t,1,$p)", ("$id", audit.Id), ("$t", ticket.Id), ("$p", Serialize(audit)));
            }
        }
        if (outcome.SupersededTicket is { } old) { var t = ReadTicket(db, tx, old); SaveTicket(db, tx, t with { Status = "Superseded", UpdatedAt = Now(), Version = t.Version + 1 }); }
        if (outcome.ClosedTicket is { } closed)
        {
            var t = ReadTicket(db, tx, closed);
            if (t.ConversationId != c.Id) throw new AdvisorException(404, "הפנייה אינה שייכת לשיחה.");
            if (t.Status is "Answered" or "Closed" or "Superseded") throw new AdvisorException(409, "הפנייה כבר השתנתה. יש לרענן לפני סגירה.");
            SaveTicket(db, tx, t with { Status = "Closed", UpdatedAt = Now(), Version = t.Version + 1 });
            var audit = new AuditEvent(Id(), t.Id, c.OwnerId, "employee", "request_cancelled", Now());
            Execute(db, tx, "INSERT INTO AuditEvent VALUES($id,$t,(SELECT COALESCE(MAX(Sequence),0)+1 FROM AuditEvent WHERE TicketId=$t),$p)", ("$id", audit.Id), ("$t", t.Id), ("$p", Serialize(audit)));
        }
        tx.Commit();
    }
    private static void UpdateConversation(SqliteConnection db, SqliteTransaction tx, Conversation c, int expected)
    {
        if (Execute(db, tx, "UPDATE Conversation SET State=$s,Title=$title,Version=Version+1,UpdatedAt=$at WHERE Id=$id AND OwnerId=$o AND Version=$v", ("$s", Serialize(c.State)), ("$title", c.Title), ("$at", Now()), ("$id", c.Id), ("$o", c.OwnerId), ("$v", expected)) != 1) throw new AdvisorException(409, "השיחה עודכנה בחלון אחר. יש לרענן ולנסות שוב.");
    }
    private static RepresentativeTicket ReadTicket(SqliteConnection db, SqliteTransaction? tx, string id) => Query(db, tx, "SELECT Payload FROM RepresentativeTicket WHERE Id=$id", r => Deserialize<RepresentativeTicket>(r.GetString(0)), ("$id", id)).SingleOrDefault() ?? throw new AdvisorException(404, "הפנייה לא נמצאה.");
    public RepresentativeTicket Ticket(string id) { using var db = Open(); return ReadTicket(db, null, id); }
    public Conversation ConversationForTicket(RepresentativeTicket ticket) { using var db = Open(); return Query(db, null, "SELECT * FROM Conversation WHERE Id=$id", MapConversation, ("$id", ticket.ConversationId)).Single(); }
    public RepresentativeTicket[] Tickets() { using var db = Open(); return Query(db, null, "SELECT Payload FROM RepresentativeTicket ORDER BY rowid DESC", r => Deserialize<RepresentativeTicket>(r.GetString(0))).ToArray(); }
    public ConversationView View(string id, string owner)
    {
        var c = RequireConversation(id, owner); using var db = Open(); var messages = Query(db, null, "SELECT Payload FROM ConversationMessage WHERE ConversationId=$id ORDER BY Sequence", r => Deserialize<ConversationMessage>(r.GetString(0)), ("$id", id)).ToArray();
        var ticket = c.State.PendingTicketId is { } t ? ReadTicket(db, null, t) : null;
        var history = Query(db, null, "SELECT Payload FROM RepresentativeTicket WHERE ConversationId=$id ORDER BY rowid DESC", r => Deserialize<RepresentativeTicket>(r.GetString(0)), ("$id", c.Id))
            .Select(t => new EmployeeTicketHistory(t, Responses(db, null, t.Id))).ToArray();
        return new(c, messages, ticket, ticket is null ? [] : Responses(db, null, ticket.Id), history, ConversationReadiness.Assess(c.State));
    }
    private static RepresentativeResponse[] Responses(SqliteConnection db, SqliteTransaction? tx, string id) => Query(db, tx, "SELECT Payload FROM RepresentativeResponse WHERE TicketId=$id ORDER BY Sequence", r => Deserialize<RepresentativeResponse>(r.GetString(0)), ("$id", id)).ToArray();
    public TicketView TicketView(string id) { using var db = Open(); return new(ReadTicket(db, null, id), Responses(db, null, id), Query(db, null, "SELECT Payload FROM AuditEvent WHERE TicketId=$id ORDER BY Sequence", r => Deserialize<AuditEvent>(r.GetString(0)), ("$id", id)).ToArray()); }
    private static void AddResponse(SqliteConnection db, SqliteTransaction tx, string ticketId, string actor, string role, string action, string text, decimal? amount, DateOnly? effectiveFrom = null)
    {
        var response = new RepresentativeResponse(Id(), ticketId, role, action, text, amount, Now(), effectiveFrom);
        Execute(db, tx, "INSERT INTO RepresentativeResponse VALUES($id,$t,(SELECT COALESCE(MAX(Sequence),0)+1 FROM RepresentativeResponse WHERE TicketId=$t),$p)", ("$id", response.Id), ("$t", ticketId), ("$p", Serialize(response)));
        var audit = new AuditEvent(Id(), ticketId, actor, role, action, Now());
        Execute(db, tx, "INSERT INTO AuditEvent VALUES($id,$t,(SELECT COALESCE(MAX(Sequence),0)+1 FROM AuditEvent WHERE TicketId=$t),$p)", ("$id", audit.Id), ("$t", ticketId), ("$p", Serialize(audit)));
    }
    public void RepresentativeUpdate(RepresentativeTicket ticket, int expectedTicketVersion, Conversation conversation, int expectedConversationVersion, ConversationMessage message, string actor, string action, string text, decimal? amount, DateOnly? effectiveFrom = null)
    {
        using var db = Open(); using var tx = db.BeginTransaction(); var current = ReadTicket(db, tx, ticket.Id);
        if (current.Version != expectedTicketVersion || current.Status is "Answered" or "Closed" or "Superseded") throw new AdvisorException(409, "הפנייה השתנתה או כבר נסגרה. יש לרענן.");
        UpdateConversation(db, tx, conversation, expectedConversationVersion); SaveTicket(db, tx, ticket with { Version = current.Version + 1, UpdatedAt = Now() });
        AddResponse(db, tx, ticket.Id, actor, "representative", action, text, amount, effectiveFrom); AddMessage(db, tx, conversation.Id, message); tx.Commit();
    }
    public void Clarify(Conversation conversation, int expected, ConversationMessage user, ConversationMessage response, string text)
    {
        using var db = Open(); using var tx = db.BeginTransaction(); var ticket = ReadTicket(db, tx, conversation.State.PendingTicketId!);
        if (ticket.Status != "ClarificationRequested") throw new AdvisorException(409, "הפנייה אינה ממתינה להבהרה.");
        UpdateConversation(db, tx, conversation, expected); SaveTicket(db, tx, ticket with { Status = "Open", Version = ticket.Version + 1, UpdatedAt = Now() });
        AddResponse(db, tx, ticket.Id, conversation.OwnerId, "employee", "clarification", text, null); AddMessage(db, tx, conversation.Id, user); AddMessage(db, tx, conversation.Id, response); tx.Commit();
    }
}
