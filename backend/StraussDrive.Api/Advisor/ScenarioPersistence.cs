namespace StraussDrive.Api.Advisor;

public sealed partial class AdvisorStore
{
    public SavedScenario[] Scenarios(string owner)
    {
        using var db = Open();
        return Query(db, null, "SELECT Id,OwnerId,ProfileKey,Name,Request,SourceFingerprint,CreatedAt FROM SavedScenario WHERE OwnerId=$o ORDER BY rowid DESC",
            r => new SavedScenario(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), Deserialize<LabScenarioRequest>(r.GetString(4)), r.GetString(5), r.GetString(6)), ("$o", owner)).ToArray();
    }
    public SavedScenario SaveScenario(EmployeeSession session, LabScenarioRequest request, string fingerprint)
    {
        using var db = Open(); using var tx = db.BeginTransaction();
        var count = Query(db, tx, "SELECT COUNT(*) FROM SavedScenario WHERE OwnerId=$o", r => r.GetInt32(0), ("$o", session.Id)).Single();
        if (count >= 12) throw new AdvisorException(422, "אפשר לשמור עד 12 תרחישים לחשבון. מחקו תרחיש שאינו נחוץ לפני שמירה נוספת.");
        var saved = new SavedScenario(Id(), session.Id, session.ProfileKey, request.Name.Trim(), request, fingerprint, Now());
        Execute(db, tx, "INSERT INTO SavedScenario VALUES($id,$o,$profile,$name,$request,$fingerprint,$at)", ("$id", saved.Id), ("$o", session.Id),
            ("$profile", session.ProfileKey), ("$name", saved.Name), ("$request", Serialize(request)), ("$fingerprint", fingerprint), ("$at", saved.CreatedAt));
        tx.Commit(); return saved;
    }
    public void RemoveScenario(string id, string owner)
    {
        using var db = Open();
        if (Execute(db, null, "DELETE FROM SavedScenario WHERE Id=$id AND OwnerId=$o", ("$id", id), ("$o", owner)) != 1)
            throw new AdvisorException(404, "התרחיש לא נמצא בחשבון שלך.");
    }
    public ConversationMessage[] OwnedQualityMessages(string owner)
    {
        using var db = Open();
        return Query(db, null, "SELECT m.Payload FROM ConversationMessage m JOIN Conversation c ON c.Id=m.ConversationId WHERE c.OwnerId=$o ORDER BY m.rowid DESC LIMIT 400", r => Deserialize<ConversationMessage>(r.GetString(0)), ("$o", owner)).ToArray();
    }
}
