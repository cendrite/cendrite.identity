using System.Text.Json;
using System.Text.Json.Serialization;

namespace Identity.Application.Caching.RecoveryChallenge;

public class RecoveryChallengeJsonConverter : JsonConverter<RecoveryChallenge>
{
    public override RecoveryChallenge Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var jsonObject = JsonDocument.ParseValue(ref reader).RootElement;
        var id = jsonObject.GetProperty("Id").GetGuid();
        var challengeValue = jsonObject.GetProperty("ChallengeValue").GetString();
        var accountPublicKey = jsonObject.GetProperty("AccountPublicKey").GetString();
        var createdAt = jsonObject.GetProperty("CreatedAt").GetDateTime();
        var expiresAt = jsonObject.GetProperty("ExpiresAt").GetDateTime();

        return RecoveryChallenge.FromCache(id, challengeValue!, accountPublicKey!, createdAt, expiresAt);
    }

    public override void Write(Utf8JsonWriter writer, RecoveryChallenge value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Id", value.Id);
        writer.WriteString("ChallengeValue", value.ChallengeValue);
        writer.WriteString("AccountPublicKey", value.AccountPublicKey);
        writer.WriteString("CreatedAt", value.CreatedAt);
        writer.WriteString("ExpiresAt", value.ExpiresAt);
        writer.WriteEndObject();
    }
}
